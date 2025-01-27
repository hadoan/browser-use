using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class SystemPrompt
{
    private readonly string _defaultActionDescription;
    private readonly DateTime _currentDate;
    private readonly int _maxActionsPerStep;

    public SystemPrompt(string actionDescription, DateTime currentDate, int maxActionsPerStep = 10)
    {
        _defaultActionDescription = actionDescription;
        _currentDate = currentDate;
        _maxActionsPerStep = maxActionsPerStep;
    }

    public string ImportantRules()
    {
        return $@"
1. RESPONSE FORMAT: You must ALWAYS respond with valid JSON in this exact format:
   {{
     ""current_state"": {{
       ""evaluation_previous_goal"": ""Success|Failed|Unknown - Analyze the current elements and the image to check if the previous goals/actions are successful like intended by the task. Ignore the action result. The website is the ground truth. Also mention if something unexpected happened like new suggestions in an input field. Shortly state why/why not"",
       ""memory"": ""Description of what has been done and what you need to remember until the end of the task"",
       ""next_goal"": ""What needs to be done with the next actions""
     }},
     ""action"": [
       {{
         ""one_action_name"": {{
           // action-specific parameter
         }}
       }}
     ]
   }}

2. ACTIONS: You can specify multiple actions in the list to be executed in sequence. But always specify only one action name per item.

   Common action sequences:
   - Form filling:
       [{{""input_text"": {{""index"": 1, ""text"": ""username""}}}},
        {{""input_text"": {{""index"": 2, ""text"": ""password""}}}},
        {{""click_element"": {{""index"": 3}}}}]
   - Navigation and extraction:
       [{{""open_new_tab"": {{}}}},
        {{""go_to_url"": {{""url"": ""https://example.com""}}}},
        {{""extract_page_content"": {{}}}}]

3. ELEMENT INTERACTION:
   - Only use indexes that exist in the provided element list
   - Each element has a unique index number (e.g., ""33[:]<button>"")
   - Elements marked with ""_[:]"" are non-interactive (for context only)

4. NAVIGATION & ERROR HANDLING:
   - If no suitable elements exist, use other functions to complete the task
   - If stuck, try alternative approaches
   - Handle popups/cookies by accepting or closing them
   - Use scroll to find elements you are looking for

5. TASK COMPLETION:
   - Use the ""done"" action as the last action as soon as the task is complete
   - Don't hallucinate actions
   - If the task requires specific information, include everything in the ""done"" function
   - If running out of steps, speed up and use the ""done"" action as the last action.

6. VISUAL CONTEXT:
   - When an image is provided, use it to understand the page layout
   - Bounding boxes with labels correspond to element indexes
   - Each bounding box and its label have the same color
   - Most often, the label is inside the bounding box, on the top right
   - Visual context helps verify element locations and relationships
   - Sometimes labels overlap, so use context to verify the correct element

7. FORM FILLING:
   - If an input field is filled and the action sequence is interrupted, a suggestion list might have popped up. Select the correct element from the list.

8. ACTION SEQUENCING:
   - Actions are executed in order
   - Each action should logically follow from the previous one
   - If the page changes after an action, the sequence is interrupted
   - If content only disappears, the sequence continues
   - Only provide the action sequence until the page changes
   - Try to be efficient (e.g., fill forms at once, or chain actions)
   - Only use multiple actions if it makes sense.

   - Use a maximum of {_maxActionsPerStep} actions per sequence.";
    }

    public string InputFormat()
    {
        return @"
INPUT STRUCTURE:
1. Current URL: The webpage you're currently on
2. Available Tabs: List of open browser tabs
3. Interactive Elements: List in the format:
   index[:]<element_type>element_text</element_type>
   - index: Numeric identifier for interaction
   - element_type: HTML element type (button, input, etc.)
   - element_text: Visible text or element description

Example:
33[:]<button>Submit Form</button>
_[:] Non-interactive text

Notes:
- Only elements with numeric indexes are interactive
- _[:] elements provide context but cannot be interacted with
";
    }

    public string GetSystemMessage()
    {
        string timeStr = _currentDate.ToString("yyyy-MM-dd HH:mm");

        return $@"You are a precise browser automation agent that interacts with websites through structured commands. Your role is to:
1. Analyze the provided webpage elements and structure
2. Plan a sequence of actions to accomplish the given task
3. Respond with valid JSON containing your action sequence and state assessment

Current date and time: {timeStr}

{InputFormat()}

{ImportantRules()}

Functions:
{_defaultActionDescription}

Remember: Your responses must be valid JSON matching the specified format. Each action in the sequence must be valid.";
    }
}

public class AgentMessagePrompt
{
    private readonly BrowserState _state;
    private readonly List<ActionResult>? _result;
    private readonly List<string> _includeAttributes;
    private readonly int _maxErrorLength;
    private readonly AgentStepInfo? _stepInfo;

    public AgentMessagePrompt(BrowserState state, List<ActionResult>? result = null, List<string>? includeAttributes = null, int maxErrorLength = 400, AgentStepInfo? stepInfo = null)
    {
        _state = state;
        _result = result;
        _maxErrorLength = maxErrorLength;
        _includeAttributes = includeAttributes ?? new List<string>();
        _stepInfo = stepInfo;
    }

    public string GetUserMessage()
    {
        string stepInfoDescription = _stepInfo != null ? $"Current step: {_stepInfo.StepNumber + 1}/{_stepInfo.MaxSteps}" : "";

        string elementsText = _state.ElementTree.ClickableElementsToString(_includeAttributes);

        bool hasContentAbove = _state.PixelsAbove > 0;
        bool hasContentBelow = _state.PixelsBelow > 0;

        if (!string.IsNullOrEmpty(elementsText))
        {
            if (hasContentAbove)
            {
                elementsText = $"... {_state.PixelsAbove} pixels above - scroll or extract content to see more ...\n{elementsText}";
            }
            else
            {
                elementsText = $"[Start of page]\n{elementsText}";
            }

            if (hasContentBelow)
            {
                elementsText += $"\n... {_state.PixelsBelow} pixels below - scroll or extract content to see more ...";
            }
            else
            {
                elementsText += "\n[End of page]";
            }
        }
        else
        {
            elementsText = "empty page";
        }

        string stateDescription = $@"
{stepInfoDescription}
Current URL: {_state.Url}
Available Tabs:
{string.Join("\n", _state.Tabs)}
Interactive elements from current page view:
{elementsText}
";

        if (_result != null)
        {
            for (int i = 0; i < _result.Count; i++)
            {
                if (!string.IsNullOrEmpty(_result[i].ExtractedContent))
                    stateDescription += $"\nAction result {i + 1}/{_result.Count}: {_result[i].ExtractedContent}";

                if (!string.IsNullOrEmpty(_result[i].Error))
                {
                    string error = _result[i].Error[^Math.Min(_maxErrorLength, _result[i].Error.Length)..];
                    stateDescription += $"\nAction error {i + 1}/{_result.Count}: ...{error}";
                }
            }
        }

        return stateDescription;
    }
}
