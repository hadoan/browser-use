using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace browser_sharp.browser;

public class BrowserConfig
{
    public bool Headless { get; set; } = false;
    public bool DisableSecurity { get; set; } = true;
    public List<string> ExtraChromiumArgs { get; set; } = new();
    public string ChromeInstancePath { get; set; } = null;
    public string WssUrl { get; set; } = null;
    public string CdpUrl { get; set; } = null;
    public ProxySettings Proxy { get; set; } = null;
    public BrowserContextConfig NewContextConfig { get; set; } = new();
}

public class Browser : IDisposable
{
    private readonly BrowserConfig _config;
    private readonly ILogger<Browser> _logger;
    private IPlaywright _playwright;
    private IBrowser _playwrightBrowser;

    private List<string> _disableSecurityArgs;

    public Browser(BrowserConfig config, ILogger<Browser> logger = null)
    {
        _config = config;
        _logger = logger;
        _disableSecurityArgs = _config.DisableSecurity
            ? new List<string>
            {
                "--disable-web-security",
                "--disable-site-isolation-trials",
                "--disable-features=IsolateOrigins,site-per-process"
            }
            : new List<string>();
    }

    public async Task<BrowserContext> NewContextAsync(BrowserContextConfig config = null)
    {
        return new BrowserContext(await GetPlaywrightBrowserAsync(), config ?? _config.NewContextConfig, _logger);
    }

    public async Task<IBrowser> GetPlaywrightBrowserAsync()
    {
        if (_playwrightBrowser == null)
        {
            return await InitializeBrowserAsync();
        }
        return _playwrightBrowser;
    }

    private async Task<IBrowser> InitializeBrowserAsync()
    {
        _logger?.LogDebug("Initializing Playwright browser...");
        _playwright = await Playwright.CreateAsync();
        _playwrightBrowser = await SetupBrowserAsync(_playwright);
        return _playwrightBrowser;
    }

    private async Task<IBrowser> SetupBrowserAsync(IPlaywright playwright)
    {
        try
        {
            if (!string.IsNullOrEmpty(_config.CdpUrl))
                return await SetupCdpAsync(playwright);

            if (!string.IsNullOrEmpty(_config.WssUrl))
                return await SetupWssAsync(playwright);

            if (!string.IsNullOrEmpty(_config.ChromeInstancePath))
                return await SetupBrowserWithInstanceAsync(playwright);

            return await SetupStandardBrowserAsync(playwright);
        }
        catch (Exception e)
        {
            _logger?.LogError($"Failed to initialize Playwright browser: {e.Message}");
            throw;
        }
    }

    private async Task<IBrowser> SetupCdpAsync(IPlaywright playwright)
    {
        if (string.IsNullOrEmpty(_config.CdpUrl))
            throw new ArgumentException("CDP URL is required.");

        _logger?.LogInformation($"Connecting to remote browser via CDP {_config.CdpUrl}");
        return await playwright.Chromium.ConnectOverCDPAsync(_config.CdpUrl);
    }

    private async Task<IBrowser> SetupWssAsync(IPlaywright playwright)
    {
        if (string.IsNullOrEmpty(_config.WssUrl))
            throw new ArgumentException("WSS URL is required.");

        _logger?.LogInformation($"Connecting to remote browser via WSS {_config.WssUrl}");
        return await playwright.Chromium.ConnectAsync(_config.WssUrl);
    }

    private async Task<IBrowser> SetupBrowserWithInstanceAsync(IPlaywright playwright)
    {
        if (string.IsNullOrEmpty(_config.ChromeInstancePath))
            throw new ArgumentException("Chrome instance path is required.");

        _logger?.LogInformation("Attempting to connect to an existing Chrome instance...");

        if (IsChromeInstanceRunning())
        {
            _logger?.LogInformation("Reusing existing Chrome instance.");
            return await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");
        }

        StartChromeInstance();
        await Task.Delay(2000);

        if (IsChromeInstanceRunning())
        {
            return await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");
        }

        _logger?.LogError("Failed to start a new Chrome instance.");
        throw new InvalidOperationException("Could not connect to Chrome instance.");
    }

    private bool IsChromeInstanceRunning()
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var response = httpClient.GetAsync("http://localhost:9222/json/version").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void StartChromeInstance()
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = _config.ChromeInstancePath,
            Arguments = "--remote-debugging-port=9222",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        Process.Start(processInfo);
        _logger?.LogInformation("Started new Chrome instance.");
    }

    private async Task<IBrowser> SetupStandardBrowserAsync(IPlaywright playwright)
    {
        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _config.Headless,
            Args = new List<string>
            {
                "--no-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--disable-background-timer-throttling",
                "--disable-popup-blocking",
                "--disable-backgrounding-occluded-windows",
                "--disable-renderer-backgrounding",
                "--disable-window-activation",
                "--disable-focus-on-load",
                "--no-first-run",
                "--no-default-browser-check",
                "--no-startup-window",
                "--window-position=0,0"
            }
            .Concat(_disableSecurityArgs)
            .Concat(_config.ExtraChromiumArgs)
            .ToArray(),
            Proxy = _config.Proxy
        });
    }

    public async Task CloseAsync()
    {
        try
        {
            if (_playwrightBrowser != null)
                await _playwrightBrowser.CloseAsync();
            if (_playwright != null)
                _playwright.Dispose();
        }
        catch (Exception e)
        {
            _logger?.LogWarning($"Failed to close Playwright browser: {e.Message}");
        }
        finally
        {
            _playwrightBrowser = null;
            _playwright = null;
        }
    }

    public void Dispose()
    {
        CloseAsync().GetAwaiter().GetResult();
    }
}
