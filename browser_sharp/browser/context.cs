using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace browser_sharp.browser;
public class BrowserContextConfig
{
    public string CookiesFile { get; set; } = null;
    public double MinimumWaitPageLoadTime { get; set; } = 0.5;
    public double WaitForNetworkIdlePageLoadTime { get; set; } = 1;
    public double MaximumWaitPageLoadTime { get; set; } = 5;
    public double WaitBetweenActions { get; set; } = 1;
    public bool DisableSecurity { get; set; } = false;
    public Dictionary<string, int> BrowserWindowSize { get; set; } = new() { { "width", 1280 }, { "height", 1100 } };
    public bool? NoViewport { get; set; } = null;
    public string SaveRecordingPath { get; set; } = null;
    public string TracePath { get; set; } = null;
    public string Locale { get; set; } = null;
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.102 Safari/537.36";
    public bool HighlightElements { get; set; } = true;
    public int ViewportExpansion { get; set; } = 500;
    public List<string> AllowedDomains { get; set; } = null;
}

public class BrowserSession
{
    public IBrowserContext Context { get; }
    public IPage CurrentPage { get; set; }
    public BrowserState CachedState { get; set; }

    public BrowserSession(IBrowserContext context, IPage currentPage, BrowserState cachedState)
    {
        Context = context;
        CurrentPage = currentPage;
        CachedState = cachedState;
    }
}

public class BrowserContext : IDisposable
{
    private readonly IBrowser _browser;
    private readonly BrowserContextConfig _config;
    private BrowserSession _session;
    private readonly ILogger<BrowserContext> _logger;

    public BrowserContext(IBrowser browser, BrowserContextConfig config, ILogger<BrowserContext> logger = null)
    {
        _browser = browser;
        _config = config;
        _logger = logger;
    }

    public async Task InitializeSessionAsync()
    {
        _logger?.LogDebug("Initializing browser context");
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        _session = new BrowserSession(context, page, new BrowserState()
        {
            Url = page.Url,
            Title = "",
            Screenshot = null,
            Tabs = new List<TabInfo>()
        });
    }

    public async Task CloseAsync()
    {
        _logger?.LogDebug("Closing browser context");
        if (_session == null) return;

        try
        {
            await SaveCookiesAsync();
            await _session.Context.CloseAsync();
        }
        catch (Exception e)
        {
            _logger?.LogDebug($"Failed to close context: {e.Message}");
        }
        finally
        {
            _session = null;
        }
    }

    public void Dispose()
    {
        CloseAsync().GetAwaiter().GetResult();
    }

    private async Task<IBrowserContext> CreateContextAsync()
    {
        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = _config.BrowserWindowSize,
            UserAgent = _config.UserAgent,
            IgnoreHTTPSErrors = _config.DisableSecurity,
            Locale = _config.Locale,
            RecordVideoDir = _config.SaveRecordingPath
        };

        var context = await _browser.NewContextAsync(contextOptions);

        if (!string.IsNullOrEmpty(_config.TracePath))
        {
            await context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }

        if (!string.IsNullOrEmpty(_config.CookiesFile) && File.Exists(_config.CookiesFile))
        {
            var cookies = JsonSerializer.Deserialize<List<BrowserContextCookies>>(File.ReadAllText(_config.CookiesFile));
            await context.AddCookiesAsync(cookies);
        }

        return context;
    }

    public async Task<IPage> GetCurrentPageAsync()
    {
        if (_session == null)
            await InitializeSessionAsync();
        return _session.CurrentPage;
    }

    public async Task SaveCookiesAsync()
    {
        if (_session?.Context == null || string.IsNullOrEmpty(_config.CookiesFile))
            return;

        try
        {
            var cookies = await _session.Context.CookiesAsync();
            File.WriteAllText(_config.CookiesFile, JsonSerializer.Serialize(cookies));
        }
        catch (Exception e)
        {
            _logger?.LogWarning($"Failed to save cookies: {e.Message}");
        }
    }

    public async Task<string> TakeScreenshotAsync(bool fullPage = false)
    {
        var page = await GetCurrentPageAsync();
        var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = fullPage });
        return Convert.ToBase64String(screenshot);
    }

    public async Task ExecuteJavaScriptAsync(string script)
    {
        var page = await GetCurrentPageAsync();
        await page.EvaluateAsync(script);
    }

    public async Task NavigateToAsync(string url)
    {
        var page = await GetCurrentPageAsync();
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
    }

    public async Task RefreshPageAsync()
    {
        var page = await GetCurrentPageAsync();
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
    }

    public async Task<string> GetPageHtmlAsync()
    {
        var page = await GetCurrentPageAsync();
        return await page.ContentAsync();
    }

    public async Task CloseCurrentTabAsync()
    {
        var session = _session;
        if (session == null) return;

        var page = session.CurrentPage;
        await page.CloseAsync();

        if (session.Context.Pages.Count > 0)
        {
            session.CurrentPage = session.Context.Pages[0];
        }
    }

    public async Task<List<TabInfo>> GetTabsInfoAsync()
    {
        var session = _session;
        if (session == null) return new List<TabInfo>();

        var tabs = new List<TabInfo>();
        for (int i = 0; i < session.Context.Pages.Count; i++)
        {
            var page = session.Context.Pages[i];
            var title = await page.TitleAsync();
            tabs.Add(new TabInfo(i, page.Url, title));
        }

        return tabs;
    }

    public async Task SwitchToTabAsync(int pageId)
    {
        var session = _session;
        if (session == null) return;

        var pages = session.Context.Pages;
        if (pageId >= pages.Count)
            throw new Exception($"No tab found with page_id: {pageId}");

        session.CurrentPage = pages[pageId];
        await session.CurrentPage.BringToFrontAsync();
    }

    public async Task CreateNewTabAsync(string url = null)
    {
        var session = _session;
        if (session == null) return;

        var newPage = await session.Context.NewPageAsync();
        session.CurrentPage = newPage;

        if (!string.IsNullOrEmpty(url))
        {
            await newPage.GotoAsync(url);
        }
    }
}
