using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.Jellyfin.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Jellyfin;

public class JellyfinPlugin : IPollingPlugin, IConfigurablePlugin, ITestablePlugin, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _seenItemIds = [];
    private readonly HashSet<string> _activeSessionIds = [];
    private bool _firstPoll = true;

    private JellyfinPluginConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.jellyfin";
    public string Name => "Jellyfin";
    public string Version => VersionUtils.Get(typeof(JellyfinPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls Jellyfin for newly added media and active playback sessions.";
    public string[] Categories => ["media", "entertainment"];
    public string Icon => "🎬";
    public Type ConfigType => typeof(JellyfinPluginConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes > 0 ? _config.PollIntervalMinutes : 5);

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
        context.Logger.LogInformation("Jellyfin plugin loaded — {Url}", _config.ServerUrl);
    }

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.ServerUrl) || string.IsNullOrEmpty(_config.ApiKey))
        {
            return;
        }

        if (_config.NotifyOnItemAdded || _firstPoll)
        {
            await PollLatestItemsAsync(context, ct);
        }

        if (_config.NotifyOnPlaybackStart || _config.NotifyOnPlaybackStop)
        {
            await PollSessionsAsync(context, ct);
        }

        _firstPoll = false;
    }

    public async Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.ServerUrl))
        {
            return new(false, "No server URL configured.");
        }

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            return new(false, "No API key configured.");
        }

        try
        {
            var json = await GetAsync("/System/Info", ct);

            if (json is null)
            {
                return new(false, "Failed to reach Jellyfin server.");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var serverName = GetString(root, "ServerName") ?? "Jellyfin";
            var version = GetString(root, "Version") ?? "?";

            return new(true, $"✓ {serverName} v{version}");
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

    private async Task PollLatestItemsAsync(IPluginContext context, CancellationToken ct)
    {
        var types = BuildItemTypeFilter();

        if (string.IsNullOrEmpty(types))
        {
            return;
        }

        var json = await GetAsync(
            $"/Items/Latest?Limit=20&Fields=Name,ProductionYear,SeriesName,ParentIndexNumber,IndexNumber&IncludeItemTypes={types}",
            ct
        );

        if (json is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(json);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var id = GetString(item, "Id");

            if (id is null)
            {
                continue;
            }

            if (!_seenItemIds.Add(id))
            {
                continue;
            }

            if (_firstPoll)
            {
                continue;
            }

            await PublishItemAddedAsync(item, context, ct);
        }
    }

    private async Task PollSessionsAsync(IPluginContext context, CancellationToken ct)
    {
        var json = await GetAsync("/Sessions?ActiveWithinSeconds=60", ct);

        if (json is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(json);
        var currentIds = new HashSet<string>();

        foreach (var session in doc.RootElement.EnumerateArray())
        {
            var sessionId = GetString(session, "Id");

            if (sessionId is null)
            {
                continue;
            }

            // only sessions that are actually playing something
            if (!session.TryGetProperty("NowPlayingItem", out _))
            {
                continue;
            }

            currentIds.Add(sessionId);

            if (!_activeSessionIds.Contains(sessionId) && !_firstPoll && _config.NotifyOnPlaybackStart)
            {
                await PublishPlaybackAsync(session, "▶️", "started watching", context, ct);
            }
        }

        if (!_firstPoll && _config.NotifyOnPlaybackStop)
        {
            foreach (var gone in _activeSessionIds.Except(currentIds))
            {
                _context?.Logger.LogDebug("Jellyfin: session {Id} ended", gone);
            }
        }

        _activeSessionIds.Clear();

        foreach (var id in currentIds)
        {
            _activeSessionIds.Add(id);
        }
    }

    private async Task PublishItemAddedAsync(JsonElement item, IPluginContext context, CancellationToken ct)
    {
        var itemType = GetString(item, "Type") ?? "";
        var name = GetString(item, "Name") ?? "Unknown";
        var year = GetString(item, "ProductionYear");

        string title;
        string body;

        if (itemType == "Episode")
        {
            var series = GetString(item, "SeriesName") ?? name;
            var season = item.TryGetProperty("ParentIndexNumber", out var s) && s.ValueKind == JsonValueKind.Number
                ? s.GetInt32() : (int?)null;
            var episode = item.TryGetProperty("IndexNumber", out var e) && e.ValueKind == JsonValueKind.Number
                ? e.GetInt32() : (int?)null;

            title = $"🎬 New episode — {series}";
            body = season is not null && episode is not null
                ? $"S{season:D2}E{episode:D2} · {name}"
                : name;
        }
        else if (itemType is "Audio" or "MusicAlbum")
        {
            var artist = GetString(item, "AlbumArtist") ?? "";
            title = "🎵 New music";
            body = string.IsNullOrEmpty(artist) ? name : $"{artist} — {name}";
        }
        else
        {
            title = "🎬 New movie";
            body = year is not null ? $"{name} ({year})" : name;
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
                    ["jellyfin.item_id"] = GetString(item, "Id") ?? "",
                    ["jellyfin.item_type"] = itemType
                }
            ),
            ct
        );
    }

    private async Task PublishPlaybackAsync(
        JsonElement session,
        string emoji,
        string verb,
        IPluginContext context,
        CancellationToken ct)
    {
        var user = GetString(session, "UserName") ?? "Someone";

        if (!session.TryGetProperty("NowPlayingItem", out var nowPlaying))
        {
            return;
        }

        var name = GetString(nowPlaying, "Name") ?? "something";
        var itemType = GetString(nowPlaying, "Type") ?? "";

        string displayName;

        if (itemType == "Episode")
        {
            var series = GetString(nowPlaying, "SeriesName") ?? name;
            var season = nowPlaying.TryGetProperty("ParentIndexNumber", out var s) && s.ValueKind == JsonValueKind.Number
                ? s.GetInt32() : (int?)null;
            var episode = nowPlaying.TryGetProperty("IndexNumber", out var e) && e.ValueKind == JsonValueKind.Number
                ? e.GetInt32() : (int?)null;

            displayName = season is not null && episode is not null
                ? $"{series} S{season:D2}E{episode:D2}"
                : $"{series} · {name}";
        }
        else
        {
            displayName = name;
        }

        await context.EventBus.PublishAsync(
            new Notification(
                Guid.NewGuid(),
                Id,
                $"{emoji} {user} {verb}",
                displayName,
                DateTimeOffset.UtcNow,
                null
            ),
            ct
        );
    }

    private string BuildItemTypeFilter()
    {
        var types = new List<string>();
        if (_config.IncludeMovies) types.Add("Movie");
        if (_config.IncludeEpisodes) types.Add("Episode");
        if (_config.IncludeMusic) types.Add("Audio");
        return string.Join(",", types);
    }

    private async Task<string?> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            var url = _config.ServerUrl.TrimEnd('/') + path;
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization",
                $"MediaBrowser Client=\"Arrr\", Device=\"Arrr\", DeviceId=\"arrr\", Version=\"1.0\", Token=\"{_config.ApiKey}\"");

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
            _context?.Logger.LogError(ex, "Jellyfin: request failed for {Path}", path);
            return null;
        }
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
