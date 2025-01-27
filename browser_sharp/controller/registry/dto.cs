using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a registered action with metadata and execution function.
/// </summary>
public class RegisteredAction
{
    public string Name { get; }
    public string Description { get; }
    public Delegate Function { get; }
    public Type ParamModel { get; }
    public bool RequiresBrowser { get; }

    public RegisteredAction(string name, string description, Delegate function, Type paramModel, bool requiresBrowser = false)
    {
        Name = name;
        Description = description;
        Function = function;
        ParamModel = paramModel;
        RequiresBrowser = requiresBrowser;
    }

    /// <summary>
    /// Returns a description of the action formatted for prompts.
    /// </summary>
    public string GetPromptDescription()
    {
        var skipKeys = new HashSet<string> { "title" };
        var paramProperties = ParamModel.GetProperties();

        var filteredParams = new Dictionary<string, object>();
        foreach (var prop in paramProperties)
        {
            if (!skipKeys.Contains(prop.Name))
            {
                filteredParams[prop.Name] = prop.PropertyType.Name;
            }
        }

        return $"{Description}: \n{{ {Name}: {JsonSerializer.Serialize(filteredParams)} }}";
    }
}

/// <summary>
/// Represents a base action model that dynamically adapts registered actions.
/// </summary>
public class ActionModel
{
    [JsonIgnore]
    private Dictionary<string, object> _actionData = new();

    public ActionModel(Dictionary<string, object> actionData)
    {
        _actionData = actionData;
    }

    /// <summary>
    /// Retrieves the index parameter if present in the action.
    /// </summary>
    public int? GetIndex()
    {
        foreach (var param in _actionData.Values)
        {
            if (param is Dictionary<string, object> paramDict && paramDict.ContainsKey("index"))
            {
                return Convert.ToInt32(paramDict["index"]);
            }
        }
        return null;
    }

    /// <summary>
    /// Sets or updates the index parameter in the action.
    /// </summary>
    public void SetIndex(int index)
    {
        var actionName = _actionData.Keys.FirstOrDefault();
        if (actionName != null && _actionData[actionName] is Dictionary<string, object> paramDict)
        {
            paramDict["index"] = index;
            _actionData[actionName] = paramDict;
        }
    }

    public Dictionary<string, object> GetActionData()
    {
        return _actionData;
    }
}

/// <summary>
/// Registry that stores and manages registered actions.
/// </summary>
public class ActionRegistry
{
    private readonly Dictionary<string, RegisteredAction> _actions = new();

    /// <summary>
    /// Registers a new action in the registry.
    /// </summary>
    public void RegisterAction(string name, string description, Delegate function, Type paramModel, bool requiresBrowser = false)
    {
        if (_actions.ContainsKey(name))
        {
            throw new InvalidOperationException($"Action '{name}' is already registered.");
        }
        _actions[name] = new RegisteredAction(name, description, function, paramModel, requiresBrowser);
    }

    /// <summary>
    /// Returns a formatted description of all registered actions.
    /// </summary>
    public string GetPromptDescription()
    {
        return string.Join("\n", _actions.Values.Select(action => action.GetPromptDescription()));
    }

    /// <summary>
    /// Retrieves a registered action by name.
    /// </summary>
    public RegisteredAction? GetAction(string name)
    {
        return _actions.TryGetValue(name, out var action) ? action : null;
    }
}
