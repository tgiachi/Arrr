using System.Text;
using Arrr.Tests.Support;
using CalDavSource;
using CalDavSource.Data;

namespace Arrr.Tests.Sources.CalDav;

[TestFixture]
public class CalDavSourcePluginTests
{
    private FakeHttpMessageHandler _httpHandler = null!;
    private FakeEventBus _eventBus = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CalDavSourcePlugin? _plugin;

    [Test]
    public async Task PollAsync_DoesNotPublish_OnFirstPoll()
    {
        // Event in 20 min, alert at 15 → triggerTime = now+5 (future) → NOT seeded
        // First poll is seed-only: nothing published regardless
        var now = _timeProvider.GetUtcNow();
        var eventStart = now.AddMinutes(20);
        _httpHandler.ResponseContent = BuildIcs("uid-1", "Standup", eventStart);

        var ctx = new FakePluginContext(
            _eventBus,
            _ => new CalDavSourceConfig
            {
                CalendarUrl = "http://fake/cal.ics",
                AlertMinutes = [15],
                LookaheadHours = 1,
                PollIntervalMinutes = 15
            }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_DoesNotPublish_WhenEventAlreadyNotified()
    {
        var now = _timeProvider.GetUtcNow();
        var eventStart = now.AddMinutes(20);
        _httpHandler.ResponseContent = BuildIcs("uid-3", "Lunch", eventStart);

        var ctx = new FakePluginContext(
            _eventBus,
            _ => new CalDavSourceConfig
            {
                CalendarUrl = "http://fake/cal.ics",
                AlertMinutes = [15],
                LookaheadHours = 1
            }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // first poll (seed)

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await _plugin.PollAsync(ctx, cts.Token); // second poll (publishes)

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _plugin.PollAsync(ctx, cts.Token); // third poll (no duplicate)

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PollAsync_PublishesNotification_WhenEventIsImminent()
    {
        // t=0: event in 20 min, triggerTime@15 = t+5 (future) → NOT seeded on first poll
        // Advance 6 min → now=t+6, triggerTime=t+5 is in (t, t+6] → publish
        var now = _timeProvider.GetUtcNow();
        var eventStart = now.AddMinutes(20);
        _httpHandler.ResponseContent = BuildIcs("uid-2", "Doctor Appointment", eventStart);

        var ctx = new FakePluginContext(
            _eventBus,
            _ => new CalDavSourceConfig
            {
                CalendarUrl = "http://fake/cal.ics",
                AlertMinutes = [15],
                LookaheadHours = 1
            }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // first poll (seed)

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await _plugin.PollAsync(ctx, cts.Token); // second poll — should notify

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var notification = (Notification)_eventBus.Published[0];
        Assert.That(notification.Title, Does.Contain("Doctor Appointment"));
        Assert.That(notification.Body, Does.Contain("15"));
    }

    [Test]
    public async Task PollAsync_RespectsMultipleAlertMinutes()
    {
        // Event in 40 min. AlertMinutes=[10,30]
        // triggerTime@30 = now+10, triggerTime@10 = now+30
        // First poll (t=0): both triggerTimes in future → NOT seeded
        // Advance 11 min: triggerTime@30 (t+10) in window → publish "30 min"
        // Advance 20 more (t=31): triggerTime@10 (t+30) in window → publish "10 min"
        var now = _timeProvider.GetUtcNow();
        var eventStart = now.AddMinutes(40);
        _httpHandler.ResponseContent = BuildIcs("uid-4", "Sprint Review", eventStart);

        var ctx = new FakePluginContext(
            _eventBus,
            _ => new CalDavSourceConfig
            {
                CalendarUrl = "http://fake/cal.ics",
                AlertMinutes = [10, 30],
                LookaheadHours = 2
            }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // first poll (seed, nothing notified)

        _timeProvider.Advance(TimeSpan.FromMinutes(11));
        await _plugin.PollAsync(ctx, cts.Token); // "30 min" alert

        _timeProvider.Advance(TimeSpan.FromMinutes(20));
        await _plugin.PollAsync(ctx, cts.Token); // "10 min" alert

        Assert.That(_eventBus.Published, Has.Count.EqualTo(2));
        var bodies = _eventBus.Published.Cast<Notification>().Select(n => n.Body).ToList();
        Assert.That(bodies.Count(b => b.Contains("30 minutes")), Is.EqualTo(1));
        Assert.That(bodies.Count(b => b.Contains("10 minutes")), Is.EqualTo(1));
    }

    [SetUp]
    public void SetUp()
    {
        _httpHandler = new();
        _eventBus = new();
        _timeProvider = new(DateTimeOffset.UtcNow);
        _plugin = new(_httpHandler, _timeProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _plugin?.Dispose();
        _plugin = null;
        _httpHandler.Dispose();
    }

    private static string BuildIcs(string uid, string summary, DateTimeOffset start, string? description = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Arrr.Tests//CalDav//EN");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid}");
        sb.AppendLine($"DTSTART:{start.UtcDateTime:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTEND:{start.UtcDateTime.AddHours(1):yyyyMMddTHHmmssZ}");
        sb.AppendLine($"SUMMARY:{summary}");

        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine($"DESCRIPTION:{description}");
        }
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");

        return sb.ToString();
    }
}
