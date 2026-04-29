using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Digest;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.Weather.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Weather;

public class WeatherPlugin : IPollingPlugin, IConfigurablePlugin, IDigestProvider, ITestablePlugin, IDisposable
{
    // WMO codes that warrant a push alert
    private static readonly HashSet<int> AlertCodes =
    [
        65, 66, 67,          // heavy / freezing rain
        71, 73, 75, 77,      // snowfall / snow grains
        80, 81, 82,          // rain showers (moderate → violent)
        85, 86,              // snow showers
        95, 96, 99           // thunderstorm (with/without hail)
    ];

    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _notifiedAlerts = [];

    private WeatherPluginConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.weather";
    public string Name => "Weather";
    public string Version => VersionUtils.Get(typeof(WeatherPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Fetches forecasts from Open-Meteo and delivers push alerts for severe conditions plus a daily digest section.";
    public string[] Categories => ["weather", "alerts"];
    public string Icon => "🌤";
    public Type ConfigType => typeof(WeatherPluginConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes > 0 ? _config.PollIntervalMinutes : 30);
    public string DigestSectionTitle => _config.DigestSectionTitle;

    public WeatherPlugin()
    {
        _httpClient = new();
    }

    internal WeatherPlugin(HttpMessageHandler handler)
    {
        _httpClient = new(handler);
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<WeatherPluginConfig>(ct);

        if (!string.IsNullOrEmpty(_config.City) && _config.Latitude == 0 && _config.Longitude == 0)
        {
            await GeocodeAsync(_config.City, ct);
        }

        context.Logger.LogInformation(
            "Weather plugin loaded — {Location} ({Lat:F4}, {Lon:F4})",
            LocationLabel(), _config.Latitude, _config.Longitude);
    }

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Latitude == 0 && _config.Longitude == 0)
        {
            return;
        }

        var json = await FetchForecastAsync(ct);

        if (json is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("hourly", out var hourly))
        {
            return;
        }

        var times = hourly.GetProperty("time").EnumerateArray().Select(t => t.GetString()!).ToList();
        var codes = hourly.GetProperty("weathercode").EnumerateArray().Select(c => c.GetInt32()).ToList();

        var now = DateTime.Now;
        var lookaheadEnd = now.AddHours(_config.AlertLookaheadHours > 0 ? _config.AlertLookaheadHours : 3);

        for (var i = 0; i < Math.Min(times.Count, codes.Count); i++)
        {
            if (!DateTime.TryParse(times[i], out var hourTime))
            {
                continue;
            }

            if (hourTime < now || hourTime > lookaheadEnd)
            {
                continue;
            }

            if (!AlertCodes.Contains(codes[i]))
            {
                continue;
            }

            // one notification per (calendar day, WMO code) — avoids spamming for multi-hour events
            var alertKey = $"{hourTime:yyyy-MM-dd}:{codes[i]}";

            if (!_notifiedAlerts.Add(alertKey))
            {
                continue;
            }

            var emoji = WmoEmoji(codes[i]);
            var desc = WmoDescription(codes[i]);
            var label = LocationLabel();

            await context.EventBus.PublishAsync(
                new Notification(
                    Guid.NewGuid(),
                    Id,
                    $"{emoji} Weather Alert — {label}",
                    $"{desc} expected at {hourTime:HH:mm}\n{label}",
                    DateTimeOffset.UtcNow,
                    null,
                    Extras: new Dictionary<string, string>
                    {
                        ["weather.location"] = label,
                        ["weather.wmo_code"] = codes[i].ToString(),
                        ["weather.alert_time"] = hourTime.ToString("O")
                    }
                ),
                ct
            );
        }
    }

    public async Task<DigestSection> GetDigestSectionAsync(DateOnly forDate, CancellationToken ct)
    {
        var section = new DigestSection { Title = DigestSectionTitle };

        if (_config.Latitude == 0 && _config.Longitude == 0)
        {
            return section;
        }

        var json = await FetchForecastAsync(ct);

        if (json is null)
        {
            return section;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("daily", out var daily))
        {
            return section;
        }

        var dates = daily.GetProperty("time").EnumerateArray().Select(t => t.GetString()!).ToList();
        var maxTemps = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(t => t.GetDouble()).ToList();
        var minTemps = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(t => t.GetDouble()).ToList();
        var dailyCodes = daily.GetProperty("weathercode").EnumerateArray().Select(c => c.GetInt32()).ToList();
        var precip = daily.GetProperty("precipitation_sum").EnumerateArray().Select(p => p.GetDouble()).ToList();

        var dateStr = forDate.ToString("yyyy-MM-dd");
        var idx = dates.IndexOf(dateStr);

        if (idx < 0)
        {
            return section;
        }

        var unit = _config.TemperatureUnit == "fahrenheit" ? "°F" : "°C";
        var emoji = WmoEmoji(dailyCodes[idx]);
        var desc = WmoDescription(dailyCodes[idx]);
        var precipStr = precip[idx] > 0 ? $" | 💧 {precip[idx]:F1}mm" : "";

        section.Items.Add(new DigestItem
        {
            Text = $"{emoji} {LocationLabel()}: {desc} | {maxTemps[idx]:F0}{unit} / {minTemps[idx]:F0}{unit}{precipStr}"
        });

        return section;
    }

    public async Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Latitude == 0 && _config.Longitude == 0 && string.IsNullOrEmpty(_config.City))
        {
            return new(false, "No location configured. Set City or Latitude/Longitude.");
        }

        if (!string.IsNullOrEmpty(_config.City) && _config.Latitude == 0 && _config.Longitude == 0)
        {
            await GeocodeAsync(_config.City, ct);

            if (_config.Latitude == 0 && _config.Longitude == 0)
            {
                return new(false, $"Could not geocode city '{_config.City}'.");
            }
        }

        var json = await FetchForecastAsync(ct);

        if (json is null)
        {
            return new(false, "Failed to fetch weather data from Open-Meteo.");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var current = doc.RootElement.GetProperty("current");
            var temp = current.GetProperty("temperature_2m").GetDouble();
            var code = current.GetProperty("weathercode").GetInt32();
            var unit = _config.TemperatureUnit == "fahrenheit" ? "°F" : "°C";

            return new(true, $"✓ {LocationLabel()}: {WmoEmoji(code)} {WmoDescription(code)}, {temp:F1}{unit}");
        }
        catch (Exception ex)
        {
            return new(false, $"Unexpected response: {ex.Message}");
        }
    }

    public void Dispose()
        => _httpClient.Dispose();

    private async Task GeocodeAsync(string city, CancellationToken ct)
    {
        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&format=json";
            var json = await _httpClient.GetStringAsync(url, ct);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("results", out var results))
            {
                return;
            }

            var arr = results.EnumerateArray().ToList();

            if (arr.Count == 0)
            {
                return;
            }

            _config.Latitude = arr[0].GetProperty("latitude").GetDouble();
            _config.Longitude = arr[0].GetProperty("longitude").GetDouble();

            if (string.IsNullOrEmpty(_config.LocationName))
            {
                _config.LocationName = arr[0].GetProperty("name").GetString() ?? city;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogWarning(ex, "Weather: geocoding failed for '{City}'", city);
        }
    }

    private async Task<string?> FetchForecastAsync(CancellationToken ct)
    {
        try
        {
            var unit = _config.TemperatureUnit == "fahrenheit" ? "fahrenheit" : "celsius";
            var url = "https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={_config.Latitude:F4}&longitude={_config.Longitude:F4}" +
                      "&current=temperature_2m,weathercode,windspeed_10m" +
                      "&hourly=weathercode,temperature_2m" +
                      "&daily=temperature_2m_max,temperature_2m_min,weathercode,precipitation_sum" +
                      $"&timezone=auto&forecast_days=2&temperature_unit={unit}";

            return await _httpClient.GetStringAsync(url, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Weather: fetch failed for {Location}", LocationLabel());
            return null;
        }
    }

    private string LocationLabel()
        => !string.IsNullOrEmpty(_config.LocationName) ? _config.LocationName : _config.City;

    private static string WmoEmoji(int code) => code switch
    {
        0 => "☀️",
        1 => "🌤",
        2 => "⛅",
        3 => "☁️",
        45 or 48 => "🌫",
        51 or 53 or 55 => "🌦",
        61 or 63 or 65 => "🌧",
        66 or 67 => "🌨",
        71 or 73 or 75 or 77 => "❄️",
        80 or 81 or 82 => "🌧",
        85 or 86 => "🌨",
        95 or 96 or 99 => "⛈",
        _ => "🌡"
    };

    private static string WmoDescription(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Rime fog",
        51 => "Light drizzle",
        53 => "Moderate drizzle",
        55 => "Dense drizzle",
        61 => "Slight rain",
        63 => "Moderate rain",
        65 => "Heavy rain",
        66 => "Light freezing rain",
        67 => "Heavy freezing rain",
        71 => "Slight snowfall",
        73 => "Moderate snowfall",
        75 => "Heavy snowfall",
        77 => "Snow grains",
        80 => "Slight rain showers",
        81 => "Moderate rain showers",
        82 => "Violent rain showers",
        85 => "Slight snow showers",
        86 => "Heavy snow showers",
        95 => "Thunderstorm",
        96 => "Thunderstorm with slight hail",
        99 => "Thunderstorm with heavy hail",
        _ => $"Weather code {code}"
    };
}
