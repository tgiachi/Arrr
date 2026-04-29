using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Digest;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.SwitchBot.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.SwitchBot;

public class SwitchBotPlugin : IPollingPlugin, IConfigurablePlugin, IDigestProvider, ITestablePlugin, IDisposable
{
    private const string ApiBase = "https://api.switch-bot.com/v1.1";

    // key: "deviceId:temp_high|temp_low|hum_high|hum_low" — one alert per sensor per threshold per day
    private readonly HashSet<string> _notifiedAlerts = [];

    private readonly HttpClient _httpClient;

    private SwitchBotPluginConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.switchbot";
    public string Name => "SwitchBot";
    public string Version => VersionUtils.Get(typeof(SwitchBotPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls SwitchBot temperature and humidity sensors and alerts when readings cross configured thresholds.";
    public string[] Categories => ["iot", "home", "sensors"];
    public string Icon => "🌡";
    public Type ConfigType => typeof(SwitchBotPluginConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes > 0 ? _config.PollIntervalMinutes : 10);
    public string DigestSectionTitle => _config.DigestSectionTitle;

    public SwitchBotPlugin()
    {
        _httpClient = new();
    }

    internal SwitchBotPlugin(HttpMessageHandler handler)
    {
        _httpClient = new(handler);
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<SwitchBotPluginConfig>(ct);
        context.Logger.LogInformation("SwitchBot plugin loaded with {Count} sensor(s)", _config.Sensors.Count);
    }

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Sensors.Count == 0 || string.IsNullOrEmpty(_config.Token))
        {
            return;
        }

        foreach (var sensor in _config.Sensors.Where(s => !string.IsNullOrEmpty(s.DeviceId)))
        {
            await PollSensorAsync(sensor, context, ct);
        }
    }

    public async Task<DigestSection> GetDigestSectionAsync(DateOnly forDate, CancellationToken ct)
    {
        var section = new DigestSection { Title = DigestSectionTitle };

        if (_config.Sensors.Count == 0 || string.IsNullOrEmpty(_config.Token))
        {
            return section;
        }

        foreach (var sensor in _config.Sensors.Where(s => !string.IsNullOrEmpty(s.DeviceId)))
        {
            var status = await FetchSensorStatusAsync(sensor.DeviceId, ct);

            if (status is null)
            {
                continue;
            }

            var label = string.IsNullOrEmpty(sensor.Name) ? sensor.DeviceId : sensor.Name;
            section.Items.Add(new DigestItem
            {
                Text = $"🌡 {label}: {status.Temperature:F1}°C  💧 {status.Humidity}%"
            });
        }

        return section;
    }

    public async Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.Token) || string.IsNullOrEmpty(_config.Secret))
        {
            return new(false, "Token and Secret are required.");
        }

        try
        {
            var json = await GetAsync("/devices", ct);

            if (json is null)
            {
                return new(false, "Failed to reach SwitchBot API.");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var statusCode = root.TryGetProperty("statusCode", out var sc) ? sc.GetInt32() : 0;

            if (statusCode != 100)
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                return new(false, $"✗ API returned {statusCode}: {msg}");
            }

            var deviceList = root.GetProperty("body").GetProperty("deviceList");
            var count = deviceList.GetArrayLength();
            var lines = new List<string> { $"✓ Connected — {count} device(s) found" };

            foreach (var sensor in _config.Sensors.Where(s => !string.IsNullOrEmpty(s.DeviceId)))
            {
                var status = await FetchSensorStatusAsync(sensor.DeviceId, ct);
                var label = string.IsNullOrEmpty(sensor.Name) ? sensor.DeviceId : sensor.Name;

                lines.Add(status is not null
                    ? $"  {label}: {status.Temperature:F1}°C  {status.Humidity}%"
                    : $"  {label}: ✗ could not read status");
            }

            return new(true, string.Join("\n", lines));
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

    private async Task PollSensorAsync(SwitchBotSensorConfig sensor, IPluginContext context, CancellationToken ct)
    {
        var status = await FetchSensorStatusAsync(sensor.DeviceId, ct);

        if (status is null)
        {
            return;
        }

        var label = string.IsNullOrEmpty(sensor.Name) ? sensor.DeviceId : sensor.Name;
        var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");

        await CheckThresholdAsync(
            sensor.DeviceId, today, "temp_high",
            sensor.TempMaxC.HasValue && status.Temperature > sensor.TempMaxC.Value,
            $"🌡 High temperature — {label}",
            $"{status.Temperature:F1}°C (threshold: {sensor.TempMaxC}°C)",
            context, ct
        );

        await CheckThresholdAsync(
            sensor.DeviceId, today, "temp_low",
            sensor.TempMinC.HasValue && status.Temperature < sensor.TempMinC.Value,
            $"🌡 Low temperature — {label}",
            $"{status.Temperature:F1}°C (threshold: {sensor.TempMinC}°C)",
            context, ct
        );

        await CheckThresholdAsync(
            sensor.DeviceId, today, "hum_high",
            sensor.HumidityMax.HasValue && status.Humidity > sensor.HumidityMax.Value,
            $"💧 High humidity — {label}",
            $"{status.Humidity}% (threshold: {sensor.HumidityMax}%)",
            context, ct
        );

        await CheckThresholdAsync(
            sensor.DeviceId, today, "hum_low",
            sensor.HumidityMin.HasValue && status.Humidity < sensor.HumidityMin.Value,
            $"💧 Low humidity — {label}",
            $"{status.Humidity}% (threshold: {sensor.HumidityMin}%)",
            context, ct
        );
    }

    private async Task CheckThresholdAsync(
        string deviceId,
        string today,
        string kind,
        bool exceeded,
        string title,
        string body,
        IPluginContext context,
        CancellationToken ct)
    {
        if (!exceeded)
        {
            return;
        }

        var key = $"{deviceId}:{today}:{kind}";

        if (!_notifiedAlerts.Add(key))
        {
            return;
        }

        await context.EventBus.PublishAsync(
            new Notification(
                Guid.NewGuid(),
                Id,
                title,
                body,
                DateTimeOffset.UtcNow,
                null,
                Extras: new Dictionary<string, string>
                {
                    ["switchbot.device_id"] = deviceId,
                    ["switchbot.threshold"] = kind
                }
            ),
            ct
        );
    }

    private async Task<SensorStatus?> FetchSensorStatusAsync(string deviceId, CancellationToken ct)
    {
        var json = await GetAsync($"/devices/{deviceId}/status", ct);

        if (json is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var body = doc.RootElement.GetProperty("body");

            var temp = body.TryGetProperty("temperature", out var t) ? t.GetDouble() : 0;
            var humidity = body.TryGetProperty("humidity", out var h) ? h.GetDouble() : 0;

            return new SensorStatus(temp, humidity);
        }
        catch (Exception ex)
        {
            _context?.Logger.LogWarning(ex, "SwitchBot: failed to parse status for {DeviceId}", deviceId);
            return null;
        }
    }

    private async Task<string?> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            var nonce = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var sign = BuildSignature(_config.Token, _config.Secret, timestamp, nonce);

            var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + path);
            request.Headers.Add("Authorization", _config.Token);
            request.Headers.Add("sign", sign);
            request.Headers.Add("nonce", nonce);
            request.Headers.Add("t", timestamp);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "SwitchBot: request failed for {Path}", path);
            return null;
        }
    }

    private static string BuildSignature(string token, string secret, string timestamp, string nonce)
    {
        var data = token + timestamp + nonce;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash).ToUpperInvariant();
    }

    private sealed record SensorStatus(double Temperature, double Humidity);
}
