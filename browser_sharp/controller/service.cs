using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class Controller
{
    private readonly ILogger<Controller> _logger;
    private readonly Registry _registry;
    private readonly Type? _outputModel;

    public Controller(ILogger<Controller> logger, List<string>? excludeActions = null, Type? outputModel = null)
    {
        _logger = logger;
        _outputModel = outputModel;
        _registry = new Registry(excludeActions ?? new List<string>());
        RegisterDefaultActions();
    }

    private void RegisterDefaultActions()
    {
        if (_outputModel != null)
        {
            _registry.RegisterAction("Complete task", _outputModel, async (BaseModel paramsModel) =>
            {
                return new ActionResult { IsDone = true, ExtractedContent = JsonSerializer.Serialize(paramsModel) };
            });
        }
        else
        {
            _registry.RegisterAction("Complete task", typeof(DoneAction), async (object parameters) =>
            {
                var doneParams = (DoneAction)parameters;
                return new ActionResult { IsDone = true, ExtractedContent = doneParams.Text };
            });
        }

        // Basic Navigation Actions
        _registry.RegisterAction("Search Google in the current tab", typeof(SearchGoogleAction), async (object parameters, BrowserContext browser) =>
        {
            var searchParams = (SearchGoogleAction)parameters;
            var page = await browser.GetCurrentPageAsync();
            await page.GoToAsync($"https://www.google.com/search?q={searchParams.Query}&udm=14");
            await page.WaitForLoadStateAsync();
            var message = $"🔍  Searched for \"{searchParams.Query}\" in Google";
            _logger.LogInformation(message);
            return new ActionResult { ExtractedContent = message, IncludeInMemory = true };
        }, requiresBrowser: true);

        _registry.RegisterAction("Navigate to URL in the current tab", typeof(GoToUrlAction), async (object parameters, BrowserContext browser) =>
        {
            var urlParams = (GoToUrlAction)parameters;
            var page = await browser.GetCurrentPageAsync();
            await page.GoToAsync(urlParams.Url);
            await page.WaitForLoadStateAsync();
            var message = $"🔗  Navigated to {urlParams.Url}";
            _logger.LogInformation(message);
            return new ActionResult { ExtractedContent = message, IncludeInMemory = true };
        }, requiresBrowser: true);
    }

    public Func<Task<List<ActionResult>>> MultiAct(List<ActionModel> actions, BrowserContext browserContext, bool checkForNewElements = true)
    {
        return async () =>
        {
            List<ActionResult> results = new();
            var session = await browserContext.GetSessionAsync();
            var cachedSelectorMap = session.CachedState.SelectorMap;
            var cachedPathHashes = cachedSelectorMap.Values.Select(e => e.Hash.BranchPathHash).ToHashSet();

            await browserContext.RemoveHighlightsAsync();

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].GetIndex() != null && i != 0)
                {
                    var newState = await browserContext.GetStateAsync();
                    var newPathHashes = newState.SelectorMap.Values.Select(e => e.Hash.BranchPathHash).ToHashSet();
                    if (checkForNewElements && !newPathHashes.IsSubsetOf(cachedPathHashes))
                    {
                        _logger.LogInformation($"New elements detected after action {i} of {actions.Count}");
                        break;
                    }
                }

                results.Add(await Act(actions[i], browserContext));

                _logger.LogDebug($"Executed action {i + 1}/{actions.Count}");
                if (results.Last().IsDone || !string.IsNullOrEmpty(results.Last().Error) || i == actions.Count - 1)
                {
                    break;
                }

                await Task.Delay(browserContext.Config.WaitBetweenActions);
            }

            return results;
        };
    }

    public async Task<ActionResult> Act(ActionModel action, BrowserContext browserContext)
    {
        try
        {
            foreach (var (actionName, paramsObj) in action.ModelDump())
            {
                if (paramsObj != null)
                {
                    var result = await _registry.ExecuteActionAsync(actionName, paramsObj, browserContext);
                    return result switch
                    {
                        string strResult => new ActionResult { ExtractedContent = strResult },
                        ActionResult actionResult => actionResult,
                        null => new ActionResult(),
                        _ => throw new InvalidOperationException($"Invalid action result type: {result.GetType()}"),
                    };
                }
            }

            return new ActionResult();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error executing action: {ex.Message}");
            return new ActionResult { Error = ex.Message };
        }
    }
}
