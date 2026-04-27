using System.Net;
using System.Text;
using Arrr.Plugin.Digest;
using Arrr.Plugin.Digest.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sources.Digest;

[TestFixture]
public class DigestSourcePluginTests
{
    private DigestFakeHttpHandler _httpHandler = null!;
    private FakeEventBus _eventBus = null!;
    private FakeTimeProvider _timeProvider = null!;
    private DigestSourcePlugin? _plugin;

    [SetUp]
    public void SetUp()
    {
        _httpHandler = new();
        _eventBus = new();
        // 06:00 UTC — GetLocalNow() derives local time from this via base class
        _timeProvider = new(new DateTimeOffset(2026, 4, 27, 6, 0, 0, TimeSpan.Zero));
        _plugin = new(_httpHandler, _timeProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _plugin?.Dispose();
        _plugin = null;
        _httpHandler.Dispose();
    }

    // ── first poll is seed-only ───────────────────────────────────────────────

    [Test]
    public async Task PollAsync_FirstPoll_NeverPublishes()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow:HH:mm}";
        var ctx = MakeContext(_eventBus, fireAt: fireAt);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── fires within window ───────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_AtFireTime_PublishesMorningDigest()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";
        _httpHandler.ResponseContent = BuildIcs("uid-1", "Team standup", localNow.AddMinutes(60));

        var ctx = MakeContext(_eventBus, fireAt: fireAt);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // first poll: seed

        _timeProvider.Advance(TimeSpan.FromMinutes(2)); // inside [fireAt, fireAt+5min)
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("Morning Digest"));
        Assert.That(n.Body, Does.Contain("Team standup"));
    }

    // ── does not fire twice same day ──────────────────────────────────────────

    [Test]
    public async Task PollAsync_InsideWindow_DoesNotFireTwice()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";
        _httpHandler.ResponseContent = BuildIcs("uid-2", "Standup", localNow.AddMinutes(60));

        var ctx = MakeContext(_eventBus, fireAt: fireAt);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await _plugin.PollAsync(ctx, cts.Token); // fires

        _timeProvider.Advance(TimeSpan.FromMinutes(1)); // still inside window
        await _plugin.PollAsync(ctx, cts.Token); // must NOT fire again

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
    }

    // ── outside fire window: does not fire ───────────────────────────────────

    [Test]
    public async Task PollAsync_AfterWindowExpired_DoesNotFire()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";

        var ctx = MakeContext(_eventBus, fireAt: fireAt);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _timeProvider.Advance(TimeSpan.FromMinutes(10)); // past window end
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── before fire window: does not fire ────────────────────────────────────

    [Test]
    public async Task PollAsync_BeforeFireWindow_DoesNotFire()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(30):HH:mm}";

        var ctx = MakeContext(_eventBus, fireAt: fireAt);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _plugin.PollAsync(ctx, cts.Token); // still 29 min before fire

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── no calendar URL: "No events." body ────────────────────────────────────

    [Test]
    public async Task PollAsync_NoCalendarUrl_PublishesWithNoEventsBody()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";

        var ctx = MakeContext(_eventBus, fireAt: fireAt, calendarUrl: "");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Body, Does.Contain("No events."));
    }

    // ── restart after window passed: key seeded, no re-fire ──────────────────

    [Test]
    public async Task PollAsync_RestartAfterWindowPassed_DoesNotRefireSameDay()
    {
        var localNow = _timeProvider.GetLocalNow();
        // FireAt 6 minutes in the past — window fully over
        var fireAt = $"{localNow.AddMinutes(-6):HH:mm}";

        var ctx = MakeContext(_eventBus, fireAt: fireAt);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seeds the already-past key

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _plugin.PollAsync(ctx, cts.Token); // key seeded → no fire

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── all-day event formatted correctly ─────────────────────────────────────

    [Test]
    public async Task PollAsync_AllDayEvent_BodyContainsAllDayLabel()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";
        var today = DateOnly.FromDateTime(localNow.DateTime);
        _httpHandler.ResponseContent = BuildAllDayIcs("uid-3", "Project kickoff", today);

        var ctx = MakeContext(_eventBus, fireAt: fireAt);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Body, Does.Contain("all day"));
        Assert.That(n.Body, Does.Contain("Project kickoff"));
    }

    // ── notification source ID ────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_PublishedNotification_HasCorrectSourceId()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";

        var ctx = MakeContext(_eventBus, fireAt: fireAt, calendarUrl: "");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await _plugin.PollAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Source, Is.EqualTo("com.arrr.plugin.digest"));
    }

    // ── notification title has emoji + label ──────────────────────────────────

    [Test]
    public async Task PollAsync_PublishedNotification_TitleContainsEmojiAndLabel()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";

        var ctx = MakeContext(_eventBus, fireAt: fireAt, calendarUrl: "", label: "Evening Digest", emoji: "🌙");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await _plugin.PollAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("🌙"));
        Assert.That(n.Title, Does.Contain("Evening Digest"));
    }

    // ── zero digests configured ───────────────────────────────────────────────

    [Test]
    public async Task PollAsync_NoDigestsConfigured_PublishesNothing()
    {
        var ctx = new FakePluginContext(_eventBus, _ => new DigestConfig { Digests = [] });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── evening digest uses day+1 ─────────────────────────────────────────────

    [Test]
    public async Task PollAsync_EveningDigest_UsesTomorrow()
    {
        var localNow = _timeProvider.GetLocalNow();
        var fireAt = $"{localNow.AddMinutes(1):HH:mm}";
        var tomorrow = DateOnly.FromDateTime(localNow.DateTime).AddDays(1);
        _httpHandler.ResponseContent = BuildAllDayIcs("uid-e1", "Tomorrow meeting", tomorrow);

        var ctx = MakeContext(_eventBus, fireAt: fireAt, calendarUrl: "http://fake/cal.ics",
            label: "Evening Digest", emoji: "🌙", dayOffset: 1, sectionHeading: "Tomorrow's Calendar");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Body, Does.Contain("Tomorrow meeting"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static FakePluginContext MakeContext(
        FakeEventBus bus,
        string fireAt = "08:00",
        string calendarUrl = "http://fake/cal.ics",
        string label = "Morning Digest",
        string emoji = "🌅",
        int dayOffset = 0,
        string sectionHeading = "Today's Calendar")
        => new(bus, _ => new DigestConfig
        {
            CalendarUrl = calendarUrl,
            PollIntervalMinutes = 1,
            Digests =
            [
                new DigestEntry
                {
                    Label = label,
                    TitleEmoji = emoji,
                    FireAt = fireAt,
                    DayOffset = dayOffset,
                    SectionHeading = sectionHeading
                }
            ]
        });

    private static string BuildIcs(string uid, string summary, DateTimeOffset start)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Arrr.Tests//Digest//EN");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid}");
        sb.AppendLine($"DTSTART:{start.UtcDateTime:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTEND:{start.UtcDateTime.AddHours(1):yyyyMMddTHHmmssZ}");
        sb.AppendLine($"SUMMARY:{summary}");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string BuildAllDayIcs(string uid, string summary, DateOnly date)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Arrr.Tests//Digest//EN");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid}");
        sb.AppendLine($"DTSTART;VALUE=DATE:{date:yyyyMMdd}");
        sb.AppendLine($"DTEND;VALUE=DATE:{date.AddDays(1):yyyyMMdd}");
        sb.AppendLine($"SUMMARY:{summary}");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private sealed class DigestFakeHttpHandler : HttpMessageHandler
    {
        public string ResponseContent { get; set; } = "";
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseContent)
            });
    }
}
