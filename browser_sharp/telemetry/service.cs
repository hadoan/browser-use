using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using dotenv.net;
using DotPostHog;

public class ProductTelemetry
{
    private static readonly Lazy<ProductTelemetry> _instance = new(() => new ProductTelemetry());
    public static ProductTelemetry Instance => _instance.Value;

    private readonly string UserIdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "browser_use", "telemetry_user_id");
    private readonly string ProjectApiKey = "phc_F8JMNjW1i2KbGUTaW1unnDdLSPCoyc52SGRU0JecaUh";
    private readonly string Host = "https://eu.i.posthog.com";
    private readonly string UnknownUserId = "UNKNOWN";

    private readonly ILogger<ProductTelemetry> _logger;
    private readonly PostHogAnalytics _posthogClient;
    private string _currentUserId;
    private bool _debugLogging;

    private ProductTelemetry()
    {
        DotEnv.Load(); // Load .env variables

        bool telemetryDisabled = Environment.GetEnvironmentVariable("ANONYMIZED_TELEMETRY")?.ToLower() == "false";
        _debugLogging = Environment.GetEnvironmentVariable("BROWSER_USE_LOGGING_LEVEL")?.ToLower() == "debug";

        if (telemetryDisabled)
        {
            _posthogClient = null;
            _logger?.LogDebug("Telemetry disabled.");
        }
        else
        {
            _logger?.LogInformation("Anonymized telemetry enabled. See https://docs.browser-use.com/development/telemetry for more information.");
            _posthogClient = PostHogAnalytics.Create(ProjectApiKey);
        }
    }

    public void Capture(BaseTelemetryEvent telemetryEvent)
    {
        if (_posthogClient == null)
            return;

        if (_debugLogging)
            _logger?.LogDebug($"Telemetry event: {telemetryEvent.Name}, Properties: {JsonSerializer.Serialize(telemetryEvent.Properties)}");

        DirectCapture(telemetryEvent);
    }

    private void DirectCapture(BaseTelemetryEvent telemetryEvent)
    {
        if (_posthogClient == null)
            return;

        try
        {
            var properties = new Dictionary<string, object>(telemetryEvent.Properties)
            {
                { "process_person_profile", true }
            };

            _posthogClient.Capture(UserId, telemetryEvent.Name, properties);
        }
        catch (Exception e)
        {
            _logger?.LogError($"Failed to send telemetry event {telemetryEvent.Name}: {e.Message}");
        }
    }

    public string UserId
    {
        get
        {
            if (!string.IsNullOrEmpty(_currentUserId))
                return _currentUserId;

            try
            {
                if (!File.Exists(UserIdPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(UserIdPath));
                    _currentUserId = Guid.NewGuid().ToString();
                    File.WriteAllText(UserIdPath, _currentUserId);
                }
                else
                {
                    _currentUserId = File.ReadAllText(UserIdPath);
                }
            }
            catch
            {
                _currentUserId = UnknownUserId;
            }

            return _currentUserId;
        }
    }
}
