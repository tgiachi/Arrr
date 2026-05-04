using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Sink.WhatsApp.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Sink.WhatsApp;

public class WhatsAppSink : ISinkPlugin, IConfigurablePlugin, ITestableSink, IDisposable
{
    private readonly HttpClient _httpClient;

    private WhatsAppSinkConfig _config = new();
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.whatsapp";
    public string Name => "WhatsApp";
    public string Version => VersionUtils.Get(typeof(WhatsAppSink));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Forwards notifications to a WhatsApp contact or group via the local whatsapp-bridge.";
    public string Icon => "💬";
    public Type ConfigType => typeof(WhatsAppSinkConfig);

    public WhatsAppSink()
    {
        _httpClient = new();
    }

    internal WhatsAppSink(HttpMessageHandler handler)
    {
        _httpClient = new(handler);
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<WhatsAppSinkConfig>(ct);
        context.Logger.LogInformation("WhatsApp sink loaded — bridge: {Url}, to: {To}", _config.BridgeUrl, _config.To);
    }

    public Task StopAsync()
        => Task.CompletedTask;

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.To))
        {
            return;
        }

        var text = _config.Template
            .Replace("{title}", notification.Title)
            .Replace("{body}", notification.Body)
            .Replace("{source}", notification.Source);

        await SendAsync(text, ct);
    }

    public async Task<PluginTestResult> TestAsync(ISinkContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.BridgeUrl))
        {
            return new(false, "No bridge URL configured.");
        }

        if (string.IsNullOrEmpty(_config.To))
        {
            return new(false, "No recipient (To) configured.");
        }

        try
        {
            var healthUrl = _config.BridgeUrl.TrimEnd('/') + "/health";
            var json = await _httpClient.GetStringAsync(healthUrl, ct);
            using var doc = JsonDocument.Parse(json);
            var connected = doc.RootElement.TryGetProperty("connected", out var c) && c.GetBoolean();

            if (!connected)
            {
                return new(false, "Bridge is running but WhatsApp is not connected.");
            }

            await SendAsync("🔔 Arrr WhatsApp sink — test message", ct);
            return new(true, $"✓ Bridge connected — test message sent to {_config.To}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new(false, $"✗ {ex.Message}");
        }
    }

    public void Dispose()
        => _httpClient.Dispose();

    private async Task SendAsync(string text, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { to = _config.To, body = text });
            var url = _config.BridgeUrl.TrimEnd('/') + "/send";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "WhatsApp sink: send failed to {To}", _config.To);
        }
    }
}
