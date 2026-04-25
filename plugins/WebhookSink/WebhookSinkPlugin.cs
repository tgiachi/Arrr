using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Microsoft.Extensions.Logging;
using WebhookSink.Data;

namespace WebhookSink;

public class WebhookSinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private readonly HttpMessageHandler? _handler;

    private HttpClient? _http;
    private WebhookSinkConfig _config = new();
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.webhook";
    public string Name => "Webhook";
    public string Version => VersionUtils.Get(typeof(WebhookSinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "POSTs notifications as JSON to any HTTP endpoint.";
    public string Icon => "🪝";
    public Type ConfigType => typeof(WebhookSinkConfig);

    public WebhookSinkPlugin() { }

    internal WebhookSinkPlugin(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_http is null || string.IsNullOrEmpty(_config.Url))
        {
            return;
        }

        var json = JsonSerializer.Serialize(notification);
        var request = new HttpRequestMessage(HttpMethod.Post, _config.Url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrEmpty(_config.AuthToken))
        {
            request.Headers.Authorization = new("Bearer", _config.AuthToken);
        }

        try
        {
            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Webhook POST failed for notification {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<WebhookSinkConfig>(ct);

        _http = _handler is not null
                    ? new(_handler)
                    : new HttpClient { Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds) };

        if (string.IsNullOrEmpty(_config.Url))
        {
            context.Logger.LogWarning("Webhook sink: Url not configured — notifications will be skipped");
        }
        else
        {
            context.Logger.LogInformation("Webhook sink ready → {Url}", _config.Url);
        }
    }

    public Task StopAsync()
    {
        _http?.Dispose();
        _http = null;

        return Task.CompletedTask;
    }
}
