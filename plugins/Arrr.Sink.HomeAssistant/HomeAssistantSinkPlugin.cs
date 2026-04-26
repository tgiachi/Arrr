using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using HomeAssistantSink.Data;
using Microsoft.Extensions.Logging;

namespace HomeAssistantSink;

public class HomeAssistantSinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private readonly HttpMessageHandler? _handler;

    private HttpClient? _http;
    private HomeAssistantSinkConfig _config = new();
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.homeassistant";
    public string Name => "Home Assistant";
    public string Version => VersionUtils.Get(typeof(HomeAssistantSinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Calls a Home Assistant notify service when a notification arrives.";
    public string Icon => "🏠";
    public Type ConfigType => typeof(HomeAssistantSinkConfig);

    public HomeAssistantSinkPlugin() { }

    internal HomeAssistantSinkPlugin(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_http is null || string.IsNullOrEmpty(_config.AccessToken))
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
            message = notification.Body
        };

        var url = $"{_config.BaseUrl.TrimEnd('/')}/api/services/notify/{_config.NotifyService}";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new("Bearer", _config.AccessToken);

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
            _context?.Logger.LogError(ex, "Home Assistant notify failed for notification {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<HomeAssistantSinkConfig>(ct);
        _http = _handler is not null ? new(_handler) : new HttpClient();

        if (string.IsNullOrEmpty(_config.AccessToken))
        {
            context.Logger.LogWarning("Home Assistant sink: AccessToken not configured — notifications will be skipped");
        }
        else
        {
            context.Logger.LogInformation(
                "Home Assistant sink ready → {BaseUrl}/api/services/notify/{Service}",
                _config.BaseUrl,
                _config.NotifyService
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
