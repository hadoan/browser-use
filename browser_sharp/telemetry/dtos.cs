using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace browser_sharp.telemetry;

public abstract class BaseTelemetryEvent
{
    public abstract string Name { get; }

    public virtual Dictionary<string, object> Properties
    {
        get
        {
            return this.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "Name")
                .ToDictionary(p => p.Name, p => p.GetValue(this));
        }
    }
}

public class RegisteredFunction
{
    public string Name { get; set; }
    public Dictionary<string, object> Params { get; set; }

    public RegisteredFunction(string name, Dictionary<string, object> parameters)
    {
        Name = name;
        Params = parameters;
    }
}

public class ControllerRegisteredFunctionsTelemetryEvent : BaseTelemetryEvent
{
    public override string Name => "controller_registered_functions";
    public List<RegisteredFunction> RegisteredFunctions { get; set; }

    public ControllerRegisteredFunctionsTelemetryEvent(List<RegisteredFunction> registeredFunctions)
    {
        RegisteredFunctions = registeredFunctions;
    }
}

public class AgentStepTelemetryEvent : BaseTelemetryEvent
{
    public override string Name => "agent_step";
    public string AgentId { get; set; }
    public int Step { get; set; }
    public List<string> StepError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public List<Dictionary<string, object>> Actions { get; set; }

    public AgentStepTelemetryEvent(string agentId, int step, List<string> stepError, int consecutiveFailures, List<Dictionary<string, object>> actions)
    {
        AgentId = agentId;
        Step = step;
        StepError = stepError;
        ConsecutiveFailures = consecutiveFailures;
        Actions = actions;
    }
}

public class AgentRunTelemetryEvent : BaseTelemetryEvent
{
    public override string Name => "agent_run";
    public string AgentId { get; set; }
    public bool UseVision { get; set; }
    public string Task { get; set; }
    public string ModelName { get; set; }
    public string ChatModelLibrary { get; set; }
    public string Version { get; set; }
    public string Source { get; set; }

    public AgentRunTelemetryEvent(string agentId, bool useVision, string task, string modelName, string chatModelLibrary, string version, string source)
    {
        AgentId = agentId;
        UseVision = useVision;
        Task = task;
        ModelName = modelName;
        ChatModelLibrary = chatModelLibrary;
        Version = version;
        Source = source;
    }
}

public class AgentEndTelemetryEvent : BaseTelemetryEvent
{
    public override string Name => "agent_end";
    public string AgentId { get; set; }
    public int Steps { get; set; }
    public bool MaxStepsReached { get; set; }
    public bool Success { get; set; }
    public List<string> Errors { get; set; }

    public AgentEndTelemetryEvent(string agentId, int steps, bool maxStepsReached, bool success, List<string> errors)
    {
        AgentId = agentId;
        Steps = steps;
        MaxStepsReached = maxStepsReached;
        Success = success;
        Errors = errors;
    }
}
