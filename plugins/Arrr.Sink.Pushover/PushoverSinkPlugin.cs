using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Sink.Pushover.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Sink.Pushover;

public class PushoverSinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private readonly HttpMessageHandler? _handler;

    private HttpClient? _http;
    private PushoverConfig _config = new();
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.pushover";
    public string Name => "Pushover";
    public string Version => VersionUtils.Get(typeof(PushoverSinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Delivers notifications to iOS and Android via Pushover.";
    public string Icon => "📱";
    public Type ConfigType => typeof(PushoverConfig);

    public PushoverSinkPlugin() { }

    internal PushoverSinkPlugin(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_http is null || string.IsNullOrEmpty(_config.ApiToken) || string.IsNullOrEmpty(_config.UserKey))
        {
            return;
        }

        var fields = new Dictionary<string, string>
        {
            ["token"] = _config.ApiToken,
            ["user"] = _config.UserKey,
            ["title"] = notification.Title,
            ["message"] = notification.Body,
            ["priority"] = notification.ToPushoverPriority().ToString()
        };

        if (!string.IsNullOrEmpty(_config.Sound))
        {
            fields["sound"] = _config.Sound;
        }

        if (!string.IsNullOrEmpty(notification.Url))
        {
            fields["url"] = notification.Url;
        }

        try
        {
            var response = await _http.PostAsync(
                               "https://api.pushover.net/1/messages.json",
                               new FormUrlEncodedContent(fields),
                               ct
                           );
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Pushover send failed for notification {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<PushoverConfig>(ct);
        _http = _handler is not null ? new(_handler) : new HttpClient();

        if (string.IsNullOrEmpty(_config.ApiToken) || string.IsNullOrEmpty(_config.UserKey))
        {
            context.Logger.LogWarning("Pushover sink: ApiToken or UserKey not configured — notifications will be skipped");
        }
        else
        {
            context.Logger.LogInformation(
                "Pushover sink ready → user {UserKey}",
                _config.UserKey[..Math.Min(4, _config.UserKey.Length)] + "****"
            );
        }
    }

    public Task StopAsync()
    {
        _http?.Dispose();
        _http = null;

        return Task.CompletedTask;
    }
}
