using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;

public class Agent
{
    private readonly string _task;
    private readonly ILogger<Agent> _logger;
    private readonly int _maxFailures;
    private readonly int _retryDelay;
    private readonly bool _useVision;
    private readonly int _maxActionsPerStep;
    private readonly List<string> _includeAttributes;
    private readonly int _maxErrorLength;
    private readonly int _maxInputTokens;
    private readonly string? _saveConversationPath;
    private readonly string? _saveConversationPathEncoding;
    
    private int _consecutiveFailures = 0;
    private int _nSteps = 1;
    private bool _paused = false;
    private bool _stopped = false;

    public string AgentId { get; } = Guid.NewGuid().ToString();
    public Browser? Browser { get; private set; }
    public BrowserContext? BrowserContext { get; private set; }
    public Controller Controller { get; }
    public AgentHistoryList History { get; private set; } = new();
    public MessageManager MessageManager { get; }

    public Func<BrowserState, AgentOutput, int, Task>? RegisterNewStepCallback { get; set; }
    public Func<AgentHistoryList, Task>? RegisterDoneCallback { get; set; }

    public Agent(
        string task,
        ILogger<Agent> logger,
        Browser? browser = null,
        BrowserContext? browserContext = null,
        Controller? controller = null,
        bool useVision = true,
        string? saveConversationPath = null,
        string? saveConversationPathEncoding = "utf-8",
        int maxFailures = 3,
        int retryDelay = 10,
        int maxInputTokens = 128000,
        bool validateOutput = false,
        List<string>? includeAttributes = null,
        int maxErrorLength = 400,
        int maxActionsPerStep = 10
    )
    {
        _task = task;
        _logger = logger;
        _maxFailures = maxFailures;
        _retryDelay = retryDelay;
        _useVision = useVision;
        _maxActionsPerStep = maxActionsPerStep;
        _includeAttributes = includeAttributes ?? new List<string>
        {
            "title", "type", "name", "role", "tabindex", "aria-label", "placeholder", "value", "alt", "aria-expanded"
        };
        _maxErrorLength = maxErrorLength;
        _maxInputTokens = maxInputTokens;
        _saveConversationPath = saveConversationPath;
        _saveConversationPathEncoding = saveConversationPathEncoding;
        
        Controller = controller ?? new Controller();

        // Initialize browser context
        Browser = browser ?? new Browser();
        BrowserContext = browserContext ?? new BrowserContext(Browser);

        MessageManager = new MessageManager(task);
    }

    public async Task RunAsync(int maxSteps = 100)
    {
        _logger.LogInformation("🚀 Starting task: {Task}", _task);

        for (int step = 0; step < maxSteps; step++)
        {
            if (_stopped || _paused) break;
            if (_consecutiveFailures >= _maxFailures)
            {
                _logger.LogError("❌ Stopping due to {MaxFailures} consecutive failures", _maxFailures);
                break;
            }

            await StepAsync();

            if (History.IsDone())
            {
                _logger.LogInformation("✅ Task completed successfully");
                if (RegisterDoneCallback != null)
                {
                    await RegisterDoneCallback(History);
                }
                break;
            }
        }
    }

    public async Task StepAsync()
    {
        _logger.LogInformation("📍 Step {Step}", _nSteps);
        BrowserState? state = null;
        AgentOutput? modelOutput = null;
        List<ActionResult> results = new();

        try
        {
            state = await BrowserContext!.GetStateAsync(_useVision);

            if (_stopped || _paused)
            {
                _logger.LogDebug("Agent paused after getting state");
                throw new TaskCanceledException();
            }

            MessageManager.AddStateMessage(state);
            var inputMessages = MessageManager.GetMessages();

            modelOutput = await GetNextActionAsync(inputMessages);

            if (RegisterNewStepCallback != null)
            {
                await RegisterNewStepCallback(state, modelOutput, _nSteps);
            }

            results = await Controller.MultiActAsync(modelOutput.Actions, BrowserContext);
            _consecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError("Step execution failed: {Message}", ex.Message);
            results.Add(new ActionResult { Error = ex.Message, IncludeInMemory = true });
            _consecutiveFailures++;
        }
        finally
        {
            if (state != null)
            {
                History.Add(new AgentHistory(modelOutput, state, results));
            }
            _nSteps++;
        }
    }

    private async Task<AgentOutput> GetNextActionAsync(List<string> inputMessages)
    {
        try
        {
            // Here we assume that some LLM-based function is used
            var outputJson = await MessageManager.InvokeLLMAsync(inputMessages);
            return JsonSerializer.Deserialize<AgentOutput>(outputJson) ?? throw new JsonException();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to parse model output: {Error}", ex.Message);
            throw new InvalidOperationException("Could not parse response.");
        }
    }

    public void Pause() => _paused = true;
    public void Resume() => _paused = false;
    public void Stop() => _stopped = true;

    public async Task SaveHistoryAsync(string filePath = "AgentHistory.json")
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, History, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<AgentHistoryList> LoadHistoryAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"History file not found: {filePath}");

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<AgentHistoryList>(fs)
            ?? throw new JsonException("Failed to deserialize AgentHistoryList");
    }
}
