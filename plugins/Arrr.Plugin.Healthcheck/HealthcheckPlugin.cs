using System.Diagnostics;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Arrr.Plugin.Healthcheck.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Healthcheck;

public class HealthcheckPlugin : IPollingPlugin, IConfigurablePlugin, IDisposable
{
    // null = never checked, true = up, false = down
    private readonly Dictionary<string, bool?> _states = [];
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    private HealthcheckConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.healthcheck";
    public string Name => "Healthcheck";
    public string Version => VersionUtils.Get(typeof(HealthcheckPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls HTTP endpoints and notifies on up/down state changes.";
    public string[] Categories => ["monitoring"];
    public string Icon => "🔍";
    public Type ConfigType => typeof(HealthcheckConfig);
    public TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(5, _config.PollIntervalSeconds));

    public HealthcheckPlugin()
    {
        _httpClient = new();
        _timeProvider = TimeProvider.System;
    }

    internal HealthcheckPlugin(HttpMessageHandler handler, TimeProvider timeProvider)
    {
        _httpClient = new(handler);
        _timeProvider = timeProvider;
    }

    public void Dispose()
        => _httpClient.Dispose();

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Endpoints.Count == 0)
        {
            return;
        }

        var tasks = _config.Endpoints.Select(e => ProbeAsync(e, context, ct));
        await Task.WhenAll(tasks);
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<HealthcheckConfig>(ct);
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _config.TimeoutSeconds));
        context.Logger.LogInformation("Healthcheck plugin loaded, monitoring {Count} endpoint(s)", _config.Endpoints.Count);
    }

    private static bool IsExpectedStatus(int statusCode, List<int> expected)
    {
        if (expected.Count == 0)
        {
            return statusCode is >= 200 and < 300;
        }

        return expected.Contains(statusCode);
    }

    private async Task ProbeAsync(EndpointConfig endpoint, IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Url))
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        bool isUp;
        string detail;

        try
        {
            using var response = await _httpClient.GetAsync(endpoint.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

            isUp = IsExpectedStatus((int)response.StatusCode, endpoint.ExpectedStatusCodes);
            detail = isUp
                         ? $"{(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms"
                         : $"unexpected {(int)response.StatusCode}";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            isUp = false;
            detail = $"timed out after {_config.TimeoutSeconds}s";
        }
        catch (Exception ex)
        {
            sw.Stop();
            isUp = false;
            detail = ex.Message;
        }

        var key = endpoint.Url;
        _states.TryGetValue(key, out var previous);

        if (previous == isUp)
        {
            return;
        }

        _states[key] = isUp;

        if (previous is null)
        {
            return; // first probe — establish baseline silently
        }

        var name = string.IsNullOrWhiteSpace(endpoint.Name) ? endpoint.Url : endpoint.Name;
        var now = _timeProvider.GetUtcNow();

        await context.EventBus.PublishAsync(
            new Notification(
                Guid.NewGuid(),
                Id,
                isUp ? $"✅ {name} recovered" : $"🔴 {name} is down",
                isUp ? $"Back online — {detail}" : $"Unreachable — {detail}",
                now,
                null,
                isUp ? NotificationPriority.Normal : NotificationPriority.Critical,
                Extras: new Dictionary<string, string>
                {
                    ["healthcheck.url"] = endpoint.Url,
                    ["healthcheck.up"] = isUp.ToString().ToLowerInvariant(),
                    ["healthcheck.detail"] = detail
                }
            ),
            ct
        );
    }
}
