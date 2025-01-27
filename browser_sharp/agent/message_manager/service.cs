using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class MessageManager
{
    private readonly ILogger<MessageManager> _logger;
    private readonly int _maxInputTokens;
    private readonly int _estimatedCharactersPerToken;
    private readonly int _imageTokens;
    private readonly int _maxErrorLength;
    private readonly int _maxActionsPerStep;
    private readonly string? _messageContext;
    private readonly SystemPrompt _systemPrompt;
    
    private int _toolId = 1;
    private readonly MessageHistory<BaseMessage> _history = new();
    
    public MessageManager(
        ILogger<MessageManager> logger,
        string task,
        string actionDescriptions,
        int maxInputTokens = 128000,
        int estimatedCharactersPerToken = 3,
        int imageTokens = 800,
        List<string>? includeAttributes = null,
        int maxErrorLength = 400,
        int maxActionsPerStep = 10,
        string? messageContext = null
    )
    {
        _logger = logger;
        _maxInputTokens = maxInputTokens;
        _estimatedCharactersPerToken = estimatedCharactersPerToken;
        _imageTokens = imageTokens;
        _maxErrorLength = maxErrorLength;
        _maxActionsPerStep = maxActionsPerStep;
        _messageContext = messageContext;

        _systemPrompt = new SystemPrompt(actionDescriptions, DateTime.Now, maxActionsPerStep);
        AddMessageWithTokens(new SystemMessage(_systemPrompt.GetSystemMessage()));

        if (!string.IsNullOrEmpty(messageContext))
        {
            AddMessageWithTokens(new HumanMessage(messageContext));
        }

        AddMessageWithTokens(TaskInstructions(task));

        AddToolMessage();
    }

    private static HumanMessage TaskInstructions(string task)
    {
        return new HumanMessage($"Your ultimate task is: {task}. If you achieved your ultimate task, stop everything and use the done action in the next step to complete the task. If not, continue as usual.");
    }

    public void AddNewTask(string newTask)
    {
        var message = new HumanMessage($"Your new ultimate task is: {newTask}. Take the previous context into account and finish your new ultimate task.");
        AddMessageWithTokens(message);
    }

    public void AddStateMessage(BrowserState state, List<ActionResult>? results = null, AgentStepInfo? stepInfo = null)
    {
        if (results != null)
        {
            foreach (var result in results)
            {
                if (result.IncludeInMemory)
                {
                    if (!string.IsNullOrEmpty(result.ExtractedContent))
                    {
                        AddMessageWithTokens(new HumanMessage($"Action result: {result.ExtractedContent}"));
                    }
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        AddMessageWithTokens(new HumanMessage($"Action error: {result.Error.Substring(Math.Max(0, result.Error.Length - _maxErrorLength))}"));
                    }
                }
            }
        }

        var stateMessage = new AgentMessagePrompt(state, results, _maxErrorLength, stepInfo).GetUserMessage();
        AddMessageWithTokens(stateMessage);
    }

    private void AddToolMessage()
    {
        var toolCalls = new
        {
            name = "AgentOutput",
            args = new
            {
                current_state = new
                {
                    evaluation_previous_goal = "Unknown - No previous actions to evaluate.",
                    memory = "",
                    next_goal = "Start browser"
                },
                action = new List<object>()
            },
            id = _toolId.ToString(),
            type = "tool_call"
        };

        AddMessageWithTokens(new AIMessage("", JsonSerializer.Serialize(toolCalls)));
        AddMessageWithTokens(new ToolMessage("Browser started", _toolId.ToString()));
        _toolId++;
    }

    public List<BaseMessage> GetMessages()
    {
        _logger.LogDebug($"Messages in history: {_history.Messages.Count}");
        int totalTokens = 0;

        foreach (var message in _history.Messages)
        {
            totalTokens += message.Metadata.InputTokens;
            _logger.LogDebug($"{message.Message.GetType().Name} - Token count: {message.Metadata.InputTokens}");
        }

        _logger.LogDebug($"Total input tokens: {totalTokens}");

        return _history.Messages.Select(m => m.Message).ToList();
    }

    private void AddMessageWithTokens(BaseMessage message)
    {
        int tokenCount = CountTokens(message);
        var metadata = new MessageMetadata(tokenCount);
        _history.AddMessage(message, metadata);
    }

    private int CountTokens(BaseMessage message)
    {
        int tokens = 0;

        if (message.Content is List<object> listContent)
        {
            foreach (var item in listContent)
            {
                if (item is Dictionary<string, string> dictItem && dictItem.ContainsKey("image_url"))
                {
                    tokens += _imageTokens;
                }
                else if (dictItem != null && dictItem.ContainsKey("text"))
                {
                    tokens += CountTextTokens(dictItem["text"]);
                }
            }
        }
        else
        {
            tokens += CountTextTokens(message.Content);
        }

        return tokens;
    }

    private int CountTextTokens(string text)
    {
        return text.Length / _estimatedCharactersPerToken;
    }

    public void CutMessages()
    {
        int excessTokens = _history.TotalTokens - _maxInputTokens;
        if (excessTokens <= 0) return;

        var lastMessage = _history.Messages.Last();

        if (lastMessage.Message.Content is List<object> listContent)
        {
            foreach (var item in listContent.OfType<Dictionary<string, string>>().ToList())
            {
                if (item.ContainsKey("image_url"))
                {
                    listContent.Remove(item);
                    lastMessage.Metadata.InputTokens -= _imageTokens;
                    _history.TotalTokens -= _imageTokens;
                    _logger.LogDebug($"Removed image with {_imageTokens} tokens");
                }
            }

            lastMessage.Message.Content = listContent;
        }

        if (_history.TotalTokens - _maxInputTokens <= 0) return;

        double proportionToRemove = (double)excessTokens / lastMessage.Metadata.InputTokens;
        if (proportionToRemove > 0.99)
        {
            throw new InvalidOperationException("Max token limit reached. Reduce the system prompt or task.");
        }

        int charactersToRemove = (int)(lastMessage.Message.Content.Length * proportionToRemove);
        lastMessage.Message.Content = lastMessage.Message.Content[..^charactersToRemove];

        _history.RemoveMessage();
        AddMessageWithTokens(new HumanMessage(lastMessage.Message.Content));
    }

    public List<BaseMessage> ConvertMessagesForNonFunctionCallingModels(List<BaseMessage> inputMessages)
    {
        return inputMessages.Select(message =>
        {
            return message switch
            {
                HumanMessage => message,
                SystemMessage => message,
                ToolMessage toolMessage => new HumanMessage(toolMessage.Content),
                AIMessage aiMessage => aiMessage.ToolCalls != null ? new AIMessage(JsonSerializer.Serialize(aiMessage.ToolCalls)) : aiMessage,
                _ => throw new InvalidOperationException($"Unknown message type: {message.GetType()}")
            };
        }).ToList();
    }

    public List<BaseMessage> MergeSuccessiveHumanMessages(List<BaseMessage> messages)
    {
        List<BaseMessage> mergedMessages = new();
        int humanStreak = 0;

        foreach (var message in messages)
        {
            if (message is HumanMessage)
            {
                humanStreak++;
                if (humanStreak > 1)
                {
                    mergedMessages[^1].Content += message.Content;
                }
                else
                {
                    mergedMessages.Add(message);
                }
            }
            else
            {
                mergedMessages.Add(message);
                humanStreak = 0;
            }
        }

        return mergedMessages;
    }

    public Dictionary<string, object>? ExtractJsonFromModelOutput(string content)
    {
        try
        {
            if (content.StartsWith("```"))
            {
                content = content.Split("```")[1];
                if (content.Contains('\n'))
                {
                    content = content.Split('\n', 2)[1];
                }
            }

            return JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse model output: {Error}", ex.Message);
            throw new InvalidOperationException("Could not parse response.");
        }
    }
}
