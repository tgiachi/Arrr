using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using GotifySink.Data;
using Microsoft.Extensions.Logging;

namespace GotifySink;

public class GotifySinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private readonly HttpMessageHandler? _handler;

    private HttpClient? _http;
    private GotifySinkConfig _config = new();
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.gotify";
    public string Name => "Gotify";
    public string Version => VersionUtils.Get(typeof(GotifySinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Publishes notifications to a self-hosted Gotify server.";
    public string Icon => "🔔";
    public Type ConfigType => typeof(GotifySinkConfig);

    public GotifySinkPlugin() { }

    internal GotifySinkPlugin(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_http is null || string.IsNullOrEmpty(_config.AppToken))
        {
            return;
        }

        var title = _config.TitleTemplate
                           .Replace("{source}", notification.Source)
                           .Replace("{title}", notification.Title)
                           .Replace("{body}", notification.Body);

        var payload = new
        {
            title,
            message  = notification.Body,
            priority = notification.ToGotifyPriority()
        };

        var url = $"{_config.ServerUrl.TrimEnd('/')}/message";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Gotify-Key", _config.AppToken);

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
            _context?.Logger.LogError(ex, "Gotify send failed for notification {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<GotifySinkConfig>(ct);
        _http = _handler is not null ? new(_handler) : new HttpClient();

        if (string.IsNullOrEmpty(_config.AppToken))
        {
            context.Logger.LogWarning("Gotify sink: AppToken not configured — notifications will be skipped");
        }
        else
        {
            context.Logger.LogInformation("Gotify sink ready → {Url}", _config.ServerUrl);
        }
    }

    public Task StopAsync()
    {
        _http?.Dispose();
        _http = null;

        return Task.CompletedTask;
    }
}
