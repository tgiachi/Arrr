using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Sink.Bark.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Sink.Bark;

public class BarkSinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private readonly HttpMessageHandler? _handler;

    private HttpClient? _http;
    private BarkConfig _config = new();
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.bark";
    public string Name => "Bark";
    public string Version => VersionUtils.Get(typeof(BarkSinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Delivers notifications to iOS via the Bark app.";
    public string Icon => "🍎";
    public Type ConfigType => typeof(BarkConfig);

    public BarkSinkPlugin() { }

    internal BarkSinkPlugin(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_http is null || string.IsNullOrEmpty(_config.DeviceKey))
        {
            return;
        }

        var payload = new
        {
            device_key = _config.DeviceKey,
            title      = notification.Title,
            body       = notification.Body,
            level      = notification.ToBarkLevel(),
            group      = _config.Group,
            sound      = string.IsNullOrEmpty(_config.Sound) ? null : _config.Sound,
            icon       = notification.IconUrl,
            url        = notification.Url
        };

        var url = $"{_config.ServerUrl.TrimEnd('/')}/push";

        try
        {
            var response = await _http.PostAsync(
                               url,
                               new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
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
            _context?.Logger.LogError(ex, "Bark send failed for notification {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<BarkConfig>(ct);
        _http = _handler is not null ? new(_handler) : new HttpClient();

        if (string.IsNullOrEmpty(_config.DeviceKey))
        {
            context.Logger.LogWarning("Bark sink: DeviceKey not configured — notifications will be skipped");
        }
        else
        {
            context.Logger.LogInformation("Bark sink ready → {Server}", _config.ServerUrl);
        }
    }

    public Task StopAsync()
    {
        _http?.Dispose();
        _http = null;

        return Task.CompletedTask;
    }
}
