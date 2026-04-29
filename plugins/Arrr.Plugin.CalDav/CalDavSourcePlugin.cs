using System.Text;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Digest;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using CalDavSource.Data;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.Extensions.Logging;

namespace CalDavSource;

public class CalDavSourcePlugin : IPollingPlugin, IConfigurablePlugin, IDigestProvider, ITestablePlugin, IDisposable
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
    public string Description => "Polls one or more CalDAV/ICS calendars and publishes upcoming event notifications.";
    public string[] Categories => ["calendar"];
    public string Icon => "📅";
    public Type ConfigType => typeof(CalDavSourceConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes > 0 ? _config.PollIntervalMinutes : 15);

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

    public string DigestSectionTitle => _config.DigestSectionTitle;

    public async Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Calendars.Count == 0)
        {
            return new(false, "No calendars configured.");
        }

        var lines = new List<string>();
        var allOk = true;

        foreach (var cal in _config.Calendars)
        {
            if (string.IsNullOrEmpty(cal.CalendarUrl))
            {
                lines.Add($"{LabelOf(cal)}: ⚠ no URL");
                allOk = false;
                continue;
            }

            try
            {
                using var request = BuildRequest(cal);
                var response = await _httpClient.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    lines.Add($"{LabelOf(cal)}: ✓ OK ({(int)response.StatusCode})");
                }
                else
                {
                    lines.Add($"{LabelOf(cal)}: ✗ {(int)response.StatusCode} {response.ReasonPhrase}");
                    allOk = false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lines.Add($"{LabelOf(cal)}: ✗ {ex.Message}");
                allOk = false;
            }
        }

        return new(allOk, string.Join("\n", lines));
    }

    public async Task<DigestSection> GetDigestSectionAsync(DateOnly forDate, CancellationToken ct)
    {
        var section = new DigestSection { Title = DigestSectionTitle };
        var targetDate = forDate.ToDateTime(TimeOnly.MinValue).Date;

        foreach (var cal in _config.Calendars.Where(c => !string.IsNullOrEmpty(c.CalendarUrl)))
        {
            var icsContent = await FetchIcsAsync(cal, ct);

            if (icsContent is null)
            {
                continue;
            }

            Calendar calendar;

            try
            {
                calendar = Calendar.Load(icsContent);
            }
            catch (Exception ex)
            {
                _context?.Logger.LogError(ex, "CalDAV: failed to parse ICS for digest ({Cal})", LabelOf(cal));
                continue;
            }

            var events = calendar.Events
                                 .Where(e => GetEventLocalDate(e) == targetDate)
                                 .OrderBy(e => e.IsAllDay ? DateTime.MinValue : e.DtStart.AsUtc.ToLocalTime())
                                 .ToList();

            var label = LabelOf(cal);

            foreach (var evt in events)
            {
                string text;

                if (evt.IsAllDay)
                {
                    text = $"[{label}] {evt.Summary} (all day)";
                }
                else
                {
                    var localTime = evt.DtStart.AsUtc.ToLocalTime();
                    text = $"[{label}] {localTime:HH:mm} - {evt.Summary}";
                }

                section.Items.Add(new() { Text = text });
            }
        }

        return section;
    }

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Calendars.Count == 0)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        foreach (var cal in _config.Calendars.Where(c => !string.IsNullOrEmpty(c.CalendarUrl)))
        {
            await PollCalendarAsync(cal, context, now, ct);
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
        context.Logger.LogInformation("CalDAV plugin loaded with {Count} calendar(s)", _config.Calendars.Count);
    }

    public void Dispose()
        => _httpClient.Dispose();

    private async Task PollCalendarAsync(
        CalDavCalendarConfig cal,
        IPluginContext context,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var icsContent = await FetchIcsAsync(cal, ct);

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
            _context?.Logger.LogError(ex, "CalDAV: failed to parse ICS content ({Cal})", LabelOf(cal));
            return;
        }

        var lookahead = now + TimeSpan.FromHours(_config.LookaheadHours);
        var calLabel = LabelOf(cal);

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
                var key = $"{calLabel}_{evt.Uid}_{alertMin}";

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
                            BuildBody(evt.Summary, evt.Description, alertMin, calLabel),
                            now,
                            null,
                            Extras: new Dictionary<string, string>
                            {
                                ["caldav.calendar"] = calLabel,
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
    }

    private static string BuildBody(string summary, string? description, int alertMinutes, string calLabel)
    {
        var desc = string.IsNullOrWhiteSpace(description) ? "" : $"\n{description}";
        return $"[{calLabel}] In {alertMinutes} minutes: {summary}{desc}";
    }

    private HttpRequestMessage BuildRequest(CalDavCalendarConfig cal)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, cal.CalendarUrl);

        if (!string.IsNullOrEmpty(cal.Username) && !string.IsNullOrEmpty(cal.Password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cal.Username}:{cal.Password}"));
            request.Headers.Authorization = new("Basic", credentials);
        }

        return request;
    }

    private async Task<string?> FetchIcsAsync(CalDavCalendarConfig cal, CancellationToken ct)
    {
        try
        {
            using var request = BuildRequest(cal);
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
            _context?.Logger.LogError(ex, "CalDAV fetch failed for {Cal}", LabelOf(cal));
            return null;
        }
    }

    private static string LabelOf(CalDavCalendarConfig cal)
        => string.IsNullOrEmpty(cal.Name) ? cal.CalendarUrl : cal.Name;

    private static DateTime GetEventLocalDate(CalendarEvent evt)
        => evt.IsAllDay
               ? evt.DtStart.Value.Date
               : evt.DtStart.AsUtc.ToLocalTime().Date;
}
