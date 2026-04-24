using System.ServiceModel.Syndication;
using System.Xml;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using RssPlugin.Data;

namespace RssPlugin;

public class RssPlugin : IPollingPlugin, IConfigurablePlugin
{
    private readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (compatible; Arrr/1.0; +https://github.com/tgiachi/Arrr)" } }
    };
    private readonly HashSet<string> _seenIds = [];

    private RssPluginConfig _config = new();
    private bool _firstPoll = true;

    public string Id => "com.arrr.rss";
    public string Name => "RSS";
    public string Version => "1.0.0";
    public string Author => "Tom";
    public string Description => "Polls RSS/Atom feeds and publishes notifications for new items.";
    public string[] Categories => ["rss", "news"];
    public string Icon => "";
    public Type ConfigType => typeof(RssPluginConfig);

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public void Dispose()
        => _httpClient.Dispose();

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        foreach (var feed in _config.Feeds)
        {
            try
            {
                await PollFeedAsync(feed, context, _firstPoll, ct);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Failed to poll feed {Url}", feed.Url);
            }
        }

        if (_firstPoll)
        {
            context.Logger.LogInformation(
                "RSS first poll complete — {Count} item(s) indexed, notifications suppressed",
                _seenIds.Count
            );
            _firstPoll = false;
        }
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _config = await context.LoadConfigAsync<RssPluginConfig>(ct);
        context.Logger.LogInformation("RSS plugin loaded {Count} feed(s)", _config.Feeds.Count);
    }

    private async Task PollFeedAsync(RssFeedConfig feed, IPluginContext context, bool seedOnly, CancellationToken ct)
    {
        var stream = await _httpClient.GetStreamAsync(feed.Url, ct);
        using var reader = XmlReader.Create(stream);
        var syndicationFeed = SyndicationFeed.Load(reader);

        foreach (var item in syndicationFeed.Items)
        {
            var itemId = item.Id ?? item.Links.FirstOrDefault()?.Uri?.ToString() ?? item.Title?.Text;

            if (itemId is null || !_seenIds.Add(itemId))
            {
                continue;
            }

            if (seedOnly)
            {
                continue;
            }

            var link = item.Links.FirstOrDefault()?.Uri?.ToString();
            var body = link is not null ? $"{feed.Label}\n{link}" : feed.Label;

            await context.EventBus.PublishAsync(
                new Notification(
                    Guid.NewGuid(),
                    Id,
                    item.Title?.Text ?? "(no title)",
                    body,
                    item.PublishDate == default ? DateTimeOffset.UtcNow : item.PublishDate,
                    null
                ),
                ct
            );
        }
    }
}
