using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class Registry
{
    private readonly ActionRegistry _registry;
    private readonly List<string> _excludeActions;
    private readonly ILogger<Registry> _logger;

    public Registry(ILogger<Registry> logger, List<string>? excludeActions = null)
    {
        _registry = new ActionRegistry();
        _excludeActions = excludeActions ?? new List<string>();
        _logger = logger;
    }

    /// <summary>
    /// Registers an action with the registry.
    /// </summary>
    public void RegisterAction<T>(string description, Func<T, Task<object>> function, bool requiresBrowser = false)
    {
        if (_excludeActions.Contains(function.Method.Name))
        {
            return;
        }

        var registeredAction = new RegisteredAction(
            function.Method.Name,
            description,
            function,
            typeof(T),
            requiresBrowser
        );

        _registry.RegisterAction(registeredAction.Name, description, function, typeof(T), requiresBrowser);
    }

    /// <summary>
    /// Executes a registered action asynchronously.
    /// </summary>
    public async Task<object?> ExecuteActionAsync(string actionName, Dictionary<string, object> parameters, BrowserContext? browser = null)
    {
        if (!_registry.GetAction(actionName, out var action))
        {
            throw new ArgumentException($"Action '{actionName}' not found.");
        }

        try
        {
            // Convert the parameters to the required type
            var validatedParams = ConvertParameters(parameters, action.ParamModel);

            if (action.RequiresBrowser)
            {
                if (browser == null)
                {
                    throw new ArgumentException($"Action '{actionName}' requires a browser context.");
                }
                return await InvokeAction(action.Function, validatedParams, browser);
            }

            return await InvokeAction(action.Function, validatedParams);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error executing action {actionName}: {ex.Message}");
            throw new InvalidOperationException($"Error executing action {actionName}", ex);
        }
    }

    /// <summary>
    /// Converts dictionary parameters to a strongly-typed object.
    /// </summary>
    private object ConvertParameters(Dictionary<string, object> parameters, Type paramType)
    {
        var json = JsonSerializer.Serialize(parameters);
        return JsonSerializer.Deserialize(json, paramType) ?? throw new InvalidOperationException("Parameter conversion failed.");
    }

    /// <summary>
    /// Dynamically invokes an action function.
    /// </summary>
    private async Task<object?> InvokeAction(Delegate function, params object?[] args)
    {
        var result = function.DynamicInvoke(args);
        return result is Task<object?> task ? await task : result;
    }

    /// <summary>
    /// Generates a description of all registered actions for prompts.
    /// </summary>
    public string GetPromptDescription()
    {
        return _registry.GetPromptDescription();
    }
}
