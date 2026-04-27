using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.Digest.Data;
using Arrr.Plugin.Digest.Internal;
using Arrr.Plugin.Digest.Providers;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Digest;

public class DigestSourcePlugin : IPollingPlugin, IConfigurablePlugin, IDisposable
{
    private static readonly TimeSpan FireWindow = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    // key = "{entry.Label}:{localDate:yyyy-MM-dd}"
    private readonly HashSet<string> _firedKeys = [];
    private bool _firstPoll = true;

    private DigestConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.digest";
    public string Name => "Digest";
    public string Version => VersionUtils.Get(typeof(DigestSourcePlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Fires morning and evening digest notifications with calendar summaries.";
    public string[] Categories => ["productivity", "calendar"];
    public string Icon => "📋";
    public Type ConfigType => typeof(DigestConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes);

    public DigestSourcePlugin()
    {
        _httpClient = new HttpClient();
        _timeProvider = TimeProvider.System;
    }

    internal DigestSourcePlugin(HttpMessageHandler handler, TimeProvider timeProvider)
    {
        _httpClient = new HttpClient(handler);
        _timeProvider = timeProvider;
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<DigestConfig>(ct);
        context.Logger.LogInformation(
            "Digest plugin loaded — {Count} digest(s) configured",
            _config.Digests.Count
        );
    }

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Digests.Count == 0)
        {
            return;
        }

        var localNow = _timeProvider.GetLocalNow();

        foreach (var entry in _config.Digests)
        {
            if (!TimeOnly.TryParse(entry.FireAt, out var fireTime))
            {
                context.Logger.LogWarning(
                    "Digest: invalid FireAt '{FireAt}' for entry '{Label}'",
                    entry.FireAt, entry.Label
                );

                continue;
            }

            var localDate = DateOnly.FromDateTime(localNow.DateTime);
            var firedKey = $"{entry.Label}:{localDate:yyyy-MM-dd}";

            if (_firstPoll)
            {
                // Seed keys for digest windows that have already passed today
                var windowEndTs = fireTime.Add(FireWindow).ToTimeSpan();

                if (localNow.TimeOfDay >= windowEndTs)
                {
                    _firedKeys.Add(firedKey);
                }

                continue;
            }

            if (_firedKeys.Contains(firedKey))
            {
                continue;
            }

            var windowStartTs = fireTime.ToTimeSpan();
            var windowEndSpan = fireTime.Add(FireWindow).ToTimeSpan();
            var currentTime = localNow.TimeOfDay;

            bool inWindow;

            if (windowStartTs <= windowEndSpan)
            {
                // Normal case (window does not cross midnight)
                inWindow = currentTime >= windowStartTs && currentTime < windowEndSpan;
            }
            else
            {
                // Window crosses midnight (e.g. 23:58 → 00:03)
                inWindow = currentTime >= windowStartTs || currentTime < windowEndSpan;
            }

            if (!inWindow)
            {
                continue;
            }

            _firedKeys.Add(firedKey);

            var forDate = localDate.AddDays(entry.DayOffset);
            var body = await BuildBodyAsync(entry, forDate, context, ct);

            await context.EventBus.PublishAsync(
                new Notification(
                    Guid.NewGuid(),
                    Id,
                    $"{entry.TitleEmoji} {entry.Label}",
                    body,
                    _timeProvider.GetUtcNow(),
                    null
                ),
                ct
            );
        }

        if (_firstPoll)
        {
            _firstPoll = false;
        }
    }

    public void Dispose()
        => _httpClient.Dispose();

    private async Task<string> BuildBodyAsync(
        DigestEntry entry,
        DateOnly forDate,
        IPluginContext context,
        CancellationToken ct)
    {
        var provider = new CalDavDigestProvider(
            _httpClient,
            _config.CalendarUrl,
            _config.CalendarUsername,
            _config.CalendarPassword,
            entry.SectionHeading,
            context.Logger
        );

        var section = await provider.GetSectionAsync(forDate, ct);

        return DigestFormatter.Format([section]);
    }
}
