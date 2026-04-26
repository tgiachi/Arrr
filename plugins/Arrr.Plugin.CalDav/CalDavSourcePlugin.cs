using System.Text;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using CalDavSource.Data;
using Ical.Net;
using Microsoft.Extensions.Logging;

namespace CalDavSource;

public class CalDavSourcePlugin : IPollingPlugin, IConfigurablePlugin, IDisposable
{
    private readonly HashSet<string> _notifiedKeys = [];
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    private CalDavSourceConfig _config = new();
    private IPluginContext? _context;
    private DateTimeOffset _lastPollTime;
    private bool _firstPoll = true;

    public string Id => "com.arrr.plugin.caldav";
    public string Name => "CalDAV";
    public string Version => VersionUtils.Get(typeof(CalDavSourcePlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls CalDAV/ICS calendars and publishes upcoming event notifications.";
    public string[] Categories => ["calendar"];
    public string Icon => "📅";
    public Type ConfigType => typeof(CalDavSourceConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes);

    public CalDavSourcePlugin()
    {
        _httpClient = new();
        _timeProvider = TimeProvider.System;
    }

    internal CalDavSourcePlugin(HttpMessageHandler handler, TimeProvider timeProvider)
    {
        _httpClient = new(handler);
        _timeProvider = timeProvider;
    }

    public void Dispose()
        => _httpClient.Dispose();

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.CalendarUrl))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var icsContent = await FetchIcsAsync(ct);

        if (icsContent is null)
        {
            return;
        }

        Calendar calendar;

        try
        {
            calendar = Calendar.Load(icsContent);
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "CalDAV: failed to parse ICS content");

            return;
        }

        var lookahead = now + TimeSpan.FromHours(_config.LookaheadHours);

        foreach (var evt in calendar.Events)
        {
            var start = new DateTimeOffset(evt.DtStart.AsUtc, TimeSpan.Zero);

            if (start < now || start > lookahead)
            {
                continue;
            }

            foreach (var alertMin in _config.AlertMinutes)
            {
                var triggerTime = start - TimeSpan.FromMinutes(alertMin);
                var key = $"{evt.Uid}_{alertMin}";

                if (_firstPoll)
                {
                    if (triggerTime <= now)
                    {
                        _notifiedKeys.Add(key);
                    }

                    continue;
                }

                if (triggerTime > _lastPollTime && triggerTime <= now && _notifiedKeys.Add(key))
                {
                    await context.EventBus.PublishAsync(
                        new Notification(
                            Guid.NewGuid(),
                            Id,
                            $"📅 {evt.Summary}",
                            BuildBody(evt.Summary, evt.Description, alertMin),
                            now,
                            null,
                            Extras: new Dictionary<string, string>
                            {
                                ["caldav.event_uid"] = evt.Uid ?? "",
                                ["caldav.event_start"] = start.ToString("O"),
                                ["caldav.alert_minutes"] = alertMin.ToString()
                            }
                        ),
                        ct
                    );
                }
            }
        }

        if (_firstPoll)
        {
            _firstPoll = false;
        }

        _lastPollTime = now;
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<CalDavSourceConfig>(ct);
        context.Logger.LogInformation("CalDAV plugin loaded, calendar: {Url}", _config.CalendarUrl);
    }

    private static string BuildBody(string summary, string? description, int alertMinutes)
    {
        var desc = string.IsNullOrWhiteSpace(description) ? "" : $"\n{description}";

        return $"In {alertMinutes} minutes: {summary}{desc}";
    }

    private async Task<string?> FetchIcsAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _config.CalendarUrl);

        if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));
            request.Headers.Authorization = new("Basic", credentials);
        }

        try
        {
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
            _context?.Logger.LogError(ex, "CalDAV fetch failed for {Url}", _config.CalendarUrl);

            return null;
        }
    }
}
