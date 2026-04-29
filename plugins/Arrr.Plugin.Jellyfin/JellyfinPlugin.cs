using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.Jellyfin.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Jellyfin;

public class JellyfinPlugin : ISourcePlugin, ICallbackPlugin, IConfigurablePlugin, ITestablePlugin, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;

    private JellyfinPluginConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.jellyfin";
    public string Name => "Jellyfin";
    public string Version => VersionUtils.Get(typeof(JellyfinPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Receives Jellyfin webhook events (new media, playback) and publishes them as notifications.";
    public string[] Categories => ["media", "entertainment"];
    public string Icon => "🎬";
    public Type ConfigType => typeof(JellyfinPluginConfig);

    public JellyfinPlugin()
    {
        _httpClient = new();
    }

    internal JellyfinPlugin(HttpMessageHandler handler)
    {
        _httpClient = new(handler);
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<JellyfinPluginConfig>(ct);
        context.Logger.LogInformation("Jellyfin plugin loaded — webhook URL: {Url}/api/plugins/{Id}/callback", context.CallbackUrl, Id);
    }

    public async Task HandleCallbackAsync(string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            _context?.Logger.LogWarning(ex, "Jellyfin: invalid JSON payload");
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            var notificationType = GetString(root, "NotificationType") ?? GetString(root, "Event") ?? "";

            switch (notificationType)
            {
                case "ItemAdded":
                    await HandleItemAddedAsync(root, ct);
                    break;

                case "PlaybackStart":
                    await HandlePlaybackAsync(root, "▶️", "started watching", ct);
                    break;

                case "PlaybackStop":
                    await HandlePlaybackAsync(root, "⏹", "stopped watching", ct);
                    break;

                default:
                    _context?.Logger.LogDebug("Jellyfin: unhandled notification type '{Type}'", notificationType);
                    break;
            }
        }
    }

    public async Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.ServerUrl))
        {
            return new(false, "No server URL configured.");
        }

        try
        {
            var url = _config.ServerUrl.TrimEnd('/') + "/System/Info/Public";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new(false, $"✗ {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var serverName = doc.RootElement.TryGetProperty("ServerName", out var name) ? name.GetString() : "Jellyfin";
            var version = doc.RootElement.TryGetProperty("Version", out var ver) ? ver.GetString() : "?";

            return new(true, $"✓ {serverName} v{version} — webhook: {context.CallbackUrl}/api/plugins/{Id}/callback");
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

    private async Task HandleItemAddedAsync(JsonElement root, CancellationToken ct)
    {
        if (!_config.NotifyOnItemAdded)
        {
            return;
        }

        var itemType = GetString(root, "ItemType") ?? "";

        if (!ShouldIncludeItemType(itemType))
        {
            return;
        }

        var name = GetString(root, "Name") ?? "Unknown";
        var year = GetString(root, "Year");
        var serverName = GetString(root, "ServerName") ?? "Jellyfin";

        string title;
        string body;

        if (itemType == "Episode")
        {
            var series = GetString(root, "SeriesName") ?? name;
            var season = GetString(root, "SeasonNumber");
            var episode = GetString(root, "EpisodeNumber");
            var epLabel = season is not null && episode is not null ? $"S{int.Parse(season):D2}E{int.Parse(episode):D2}" : "";
            title = $"🎬 New episode — {series}";
            body = string.IsNullOrEmpty(epLabel) ? $"{name}\n{serverName}" : $"{epLabel} · {name}\n{serverName}";
        }
        else if (itemType == "Audio" || itemType == "MusicAlbum")
        {
            var artist = GetString(root, "Artists") ?? GetString(root, "AlbumArtist") ?? "";
            title = $"🎵 New music — {serverName}";
            body = string.IsNullOrEmpty(artist) ? name : $"{artist} — {name}";
        }
        else
        {
            title = $"🎬 New on {serverName}";
            body = year is not null ? $"{name} ({year})" : name;
        }

        await PublishAsync(title, body, ct);
    }

    private async Task HandlePlaybackAsync(JsonElement root, string emoji, string verb, CancellationToken ct)
    {
        if (verb == "started watching" && !_config.NotifyOnPlaybackStart)
        {
            return;
        }

        if (verb == "stopped watching" && !_config.NotifyOnPlaybackStop)
        {
            return;
        }

        var user = GetString(root, "NotificationUsername") ?? GetString(root, "UserName") ?? "Someone";
        var name = GetString(root, "Name") ?? "something";
        var itemType = GetString(root, "ItemType") ?? "";

        string displayName;

        if (itemType == "Episode")
        {
            var series = GetString(root, "SeriesName") ?? name;
            var season = GetString(root, "SeasonNumber");
            var episode = GetString(root, "EpisodeNumber");
            displayName = season is not null && episode is not null
                ? $"{series} S{int.Parse(season):D2}E{int.Parse(episode):D2}"
                : $"{series} · {name}";
        }
        else
        {
            displayName = name;
        }

        await PublishAsync(
            $"{emoji} {user} {verb}",
            displayName,
            ct
        );
    }

    private bool ShouldIncludeItemType(string itemType) => itemType switch
    {
        "Movie" => _config.IncludeMovies,
        "Episode" => _config.IncludeEpisodes,
        "Audio" or "MusicAlbum" => _config.IncludeMusic,
        _ => false
    };

    private async Task PublishAsync(string title, string body, CancellationToken ct)
    {
        if (_context is null)
        {
            return;
        }

        await _context.EventBus.PublishAsync(
            new Notification(
                Guid.NewGuid(),
                Id,
                title,
                body,
                DateTimeOffset.UtcNow,
                null,
                Extras: new Dictionary<string, string>
                {
                    ["jellyfin.server"] = _config.ServerUrl
                }
            ),
            ct
        );
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetRawText();
        }

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }
}
