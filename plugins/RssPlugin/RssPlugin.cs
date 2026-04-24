using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using RssPlugin.Data;

namespace RssPlugin;

public class RssPlugin : IPollingPlugin
{
    private readonly HttpClient _httpClient = new();
    private readonly HashSet<string> _seenIds = [];

    private RssPluginConfig _config = new([]);

    public string Id          => "com.arrr.rss";
    public string Name        => "RSS";
    public string Version     => "1.0.0";
    public string Author      => "Tom";
    public string Description => "Polls RSS/Atom feeds and publishes notifications for new items.";
    public string[] Categories => ["rss", "news"];
    public string Icon        => "";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        if (!File.Exists(context.ConfigPath))
        {
            context.Logger.LogWarning("RSS config not found at {Path} — no feeds will be polled", context.ConfigPath);
            return;
        }

        await using var stream = File.OpenRead(context.ConfigPath);
        _config = await JsonSerializer.DeserializeAsync<RssPluginConfig>(stream, cancellationToken: ct)
                  ?? new([]);

        context.Logger.LogInformation("RSS plugin loaded {Count} feed(s)", _config.Feeds.Count);
    }

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        foreach (var feed in _config.Feeds)
        {
            try
            {
                await PollFeedAsync(feed, context, ct);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Failed to poll feed {Url}", feed.Url);
            }
        }
    }

    private async Task PollFeedAsync(RssFeedConfig feed, IPluginContext context, CancellationToken ct)
    {
        var stream = await _httpClient.GetStreamAsync(feed.Url, ct);
        using var reader = XmlReader.Create(stream);
        var syndicationFeed = SyndicationFeed.Load(reader);

        foreach (var item in syndicationFeed.Items)
        {
            var itemId = item.Id
                         ?? item.Links.FirstOrDefault()?.Uri?.ToString()
                         ?? item.Title?.Text;

            if (itemId is null || !_seenIds.Add(itemId))
                continue;

            var link = item.Links.FirstOrDefault()?.Uri?.ToString();
            var body = link is not null ? $"{feed.Label}\n{link}" : feed.Label;

            await context.EventBus.PublishAsync(
                new Notification(
                    Id:        Guid.NewGuid(),
                    Source:    Id,
                    Title:     item.Title?.Text ?? "(no title)",
                    Body:      body,
                    Timestamp: item.PublishDate == default ? DateTimeOffset.UtcNow : item.PublishDate,
                    IconUrl:   null
                ),
                ct
            );
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
