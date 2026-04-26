using System.Text;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Microsoft.Extensions.Logging;
using NtfySink.Data;

namespace NtfySink;

public class NtfySinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private readonly HttpMessageHandler? _handler;

    private HttpClient? _http;
    private NtfySinkConfig _config = new();
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.ntfy";
    public string Name => "Ntfy";
    public string Version => VersionUtils.Get(typeof(NtfySinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Publishes notifications to a ntfy topic via HTTP.";
    public string Icon => "🔔";
    public Type ConfigType => typeof(NtfySinkConfig);

    public NtfySinkPlugin() { }

    internal NtfySinkPlugin(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_http is null || string.IsNullOrEmpty(_config.Topic))
        {
            return;
        }

        var url = $"{_config.ServerUrl.TrimEnd('/')}/{_config.Topic}";
        var title = _config.TitleTemplate
                           .Replace("{source}", notification.Source)
                           .Replace("{title}", notification.Title)
                           .Replace("{body}", notification.Body);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(notification.Body, Encoding.UTF8, "text/plain")
        };

        request.Headers.Add("X-Title", title);
        request.Headers.Add("X-Priority", notification.ToNtfyPriority());

        if (!string.IsNullOrEmpty(notification.Url))
        {
            request.Headers.Add("X-Click", notification.Url);
        }

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
            _context?.Logger.LogError(ex, "Ntfy send failed for notification {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<NtfySinkConfig>(ct);
        _http = _handler is not null ? new(_handler) : new HttpClient();

        if (string.IsNullOrEmpty(_config.Topic))
        {
            context.Logger.LogWarning("Ntfy sink: Topic not configured — notifications will be skipped");
        }
        else
        {
            context.Logger.LogInformation("Ntfy sink ready → {Url}/{Topic}", _config.ServerUrl, _config.Topic);
        }
    }

    public Task StopAsync()
    {
        _http?.Dispose();
        _http = null;

        return Task.CompletedTask;
    }
}
