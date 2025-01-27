using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public record AgentStepInfo(int StepNumber, int MaxSteps);

public class ActionResult
{
    public bool? IsDone { get; set; } = false;
    public string? ExtractedContent { get; set; } = null;
    public string? Error { get; set; } = null;
    public bool IncludeInMemory { get; set; } = false;
}

public class AgentBrain
{
    public string EvaluationPreviousGoal { get; set; } = string.Empty;
    public string Memory { get; set; } = string.Empty;
    public string NextGoal { get; set; } = string.Empty;
}

public class AgentOutput
{
    public AgentBrain CurrentState { get; set; } = new();
    public List<ActionModel> Actions { get; set; } = new();

    public static Type TypeWithCustomActions<T>() where T : ActionModel
    {
        return typeof(AgentOutput<T>);
    }
}

public class AgentOutput<T> : AgentOutput where T : ActionModel
{
    public new List<T> Actions { get; set; } = new();
}

public class AgentHistory
{
    public AgentOutput? ModelOutput { get; set; }
    public List<ActionResult> Results { get; set; } = new();
    public BrowserStateHistory State { get; set; } = new();

    public static List<DOMHistoryElement?> GetInteractedElement(AgentOutput modelOutput, SelectorMap selectorMap)
    {
        var elements = new List<DOMHistoryElement?>();

        foreach (var action in modelOutput.Actions)
        {
            var index = action.GetIndex();
            if (index.HasValue && selectorMap.ContainsKey(index.Value))
            {
                DOMElementNode el = selectorMap[index.Value];
                elements.Add(HistoryTreeProcessor.ConvertDomElementToHistoryElement(el));
            }
            else
            {
                elements.Add(null);
            }
        }

        return elements;
    }

    public Dictionary<string, object?> Serialize()
    {
        return new()
        {
            { "model_output", ModelOutput != null ? SerializeModelOutput(ModelOutput) : null },
            { "result", Results.Select(r => r.Serialize()).ToList() },
            { "state", State.ToDictionary() }
        };
    }

    private static Dictionary<string, object?> SerializeModelOutput(AgentOutput modelOutput)
    {
        return new()
        {
            { "current_state", modelOutput.CurrentState },
            { "action", modelOutput.Actions.Select(a => a.Serialize()).ToList() }
        };
    }
}

public class AgentHistoryList
{
    public List<AgentHistory> History { get; set; } = new();

    public override string ToString()
    {
        return $"AgentHistoryList(all_results={ActionResults()}, all_model_outputs={ModelActions()})";
    }

    public void SaveToFile(string filepath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filepath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filepath, json);
        }
        catch (Exception ex)
        {
            throw new Exception("Error saving file", ex);
        }
    }

    public static AgentHistoryList LoadFromFile<T>(string filepath) where T : AgentOutput
    {
        if (!File.Exists(filepath))
            throw new FileNotFoundException("File not found", filepath);

        var json = File.ReadAllText(filepath);
        var data = JsonSerializer.Deserialize<AgentHistoryList>(json);

        if (data == null) throw new Exception("Failed to deserialize AgentHistoryList");

        foreach (var history in data.History)
        {
            if (history.ModelOutput != null && history.ModelOutput is Dictionary<string, object> dict)
            {
                history.ModelOutput = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(dict));
            }

            if (!history.State.ToDictionary().ContainsKey("interacted_element"))
            {
                history.State.ToDictionary()["interacted_element"] = null;
            }
        }

        return data;
    }

    public Dictionary<string, object>? LastAction()
    {
        if (History.Any() && History.Last().ModelOutput != null)
        {
            return History.Last().ModelOutput.Actions.Last().Serialize();
        }
        return null;
    }

    public List<string> Errors()
    {
        return History
            .SelectMany(h => h.Results.Where(r => !string.IsNullOrEmpty(r.Error)).Select(r => r.Error!))
            .ToList();
    }

    public string? FinalResult()
    {
        return History.Any() && History.Last().Results.Any()
            ? History.Last().Results.Last().ExtractedContent
            : null;
    }

    public bool IsDone()
    {
        return History.Any() &&
               History.Last().Results.Any() &&
               History.Last().Results.Last().IsDone == true;
    }

    public bool HasErrors()
    {
        return Errors().Count > 0;
    }

    public List<string> Urls()
    {
        return History.Where(h => !string.IsNullOrEmpty(h.State.Url)).Select(h => h.State.Url!).ToList();
    }

    public List<string> Screenshots()
    {
        return History.Where(h => !string.IsNullOrEmpty(h.State.Screenshot)).Select(h => h.State.Screenshot!).ToList();
    }

    public List<string> ActionNames()
    {
        return ModelActions().Select(a => a.Keys.First()).ToList();
    }

    public List<AgentBrain> ModelThoughts()
    {
        return History.Where(h => h.ModelOutput != null).Select(h => h.ModelOutput.CurrentState).ToList();
    }

    public List<AgentOutput> ModelOutputs()
    {
        return History.Where(h => h.ModelOutput != null).Select(h => h.ModelOutput).ToList();
    }

    public List<Dictionary<string, object>> ModelActions()
    {
        return History
            .Where(h => h.ModelOutput != null)
            .SelectMany(h => h.ModelOutput.Actions.Select(a => a.Serialize()))
            .ToList();
    }

    public List<ActionResult> ActionResults()
    {
        return History.SelectMany(h => h.Results).ToList();
    }

    public List<string> ExtractedContent()
    {
        return History.SelectMany(h => h.Results)
                      .Where(r => !string.IsNullOrEmpty(r.ExtractedContent))
                      .Select(r => r.ExtractedContent!)
                      .ToList();
    }

    public List<Dictionary<string, object>> ModelActionsFiltered(List<string> include)
    {
        return ModelActions().Where(a => include.Contains(a.Keys.First())).ToList();
    }
}

public static class AgentError
{
    public const string VALIDATION_ERROR = "Invalid model output format. Please follow the correct schema.";
    public const string RATE_LIMIT_ERROR = "Rate limit reached. Waiting before retry.";
    public const string NO_VALID_ACTION = "No valid action found";

    public static string FormatError(Exception error, bool includeTrace = false)
    {
        return includeTrace ? $"{error.Message}\nStacktrace:\n{error.StackTrace}" : error.Message;
    }
}
