using System.Net;
using System.Text;
using Arrr.Tests.Support;
using CalDavSource;
using CalDavSource.Data;

namespace Arrr.Tests.Sources.CalDav;

[TestFixture]
public class CalDavDigestProviderTests
{
    private FakeHttpMessageHandler _httpHandler = null!;
    private FakeEventBus _eventBus = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CalDavSourcePlugin? _plugin;

    [SetUp]
    public void SetUp()
    {
        _httpHandler = new();
        _eventBus = new();
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

    // ── empty calendar URL ────────────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_EmptyCalendarUrl_ReturnsEmptySection()
    {
        var ctx = MakeContext(calendarUrl: "");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Is.Empty);
    }

    // ── timed event today ─────────────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_TimedEventToday_AppearsWithTime()
    {
        var localNow = _timeProvider.GetLocalNow();
        var eventStart = localNow.AddHours(2); // 08:00 local
        _httpHandler.ResponseContent = BuildIcs("uid-1", "Standup", eventStart);

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var today = DateOnly.FromDateTime(localNow.DateTime);
        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Has.Count.EqualTo(1));
        Assert.That(section.Items[0].Text, Does.Contain("Standup"));
        Assert.That(section.Items[0].Text, Does.Contain(":"));  // HH:mm format
    }

    // ── all-day event today ───────────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_AllDayEventToday_HasAllDayLabel()
    {
        var localNow = _timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(localNow.DateTime);
        _httpHandler.ResponseContent = BuildAllDayIcs("uid-2", "Project kickoff", today);

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Has.Count.EqualTo(1));
        Assert.That(section.Items[0].Text, Does.Contain("all day"));
        Assert.That(section.Items[0].Text, Does.Contain("Project kickoff"));
    }

    // ── event tomorrow excluded when forDate is today ─────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_EventTomorrow_ExcludedWhenForDateIsToday()
    {
        var localNow = _timeProvider.GetLocalNow();
        var tomorrow = DateOnly.FromDateTime(localNow.DateTime).AddDays(1);
        _httpHandler.ResponseContent = BuildAllDayIcs("uid-3", "Tomorrow event", tomorrow);

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var today = DateOnly.FromDateTime(localNow.DateTime);
        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Is.Empty);
    }

    // ── event tomorrow included when forDate is tomorrow ─────────────────────

    [Test]
    public async Task GetDigestSectionAsync_EventTomorrow_IncludedWhenForDateIsTomorrow()
    {
        var localNow = _timeProvider.GetLocalNow();
        var tomorrow = DateOnly.FromDateTime(localNow.DateTime).AddDays(1);
        _httpHandler.ResponseContent = BuildAllDayIcs("uid-4", "Team offsite", tomorrow);

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var section = await _plugin.GetDigestSectionAsync(tomorrow, cts.Token);

        Assert.That(section.Items, Has.Count.EqualTo(1));
        Assert.That(section.Items[0].Text, Does.Contain("Team offsite"));
    }

    // ── section title from config ─────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_SectionTitleFromConfig()
    {
        var ctx = MakeContext(sectionTitle: "Tomorrow's Calendar");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        Assert.That(_plugin.DigestSectionTitle, Is.EqualTo("Tomorrow's Calendar"));
    }

    // ── HTTP error → empty section ────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_HttpError_ReturnsEmptySection()
    {
        _httpHandler.ResponseStatusCode = HttpStatusCode.InternalServerError;

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Is.Empty);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private FakePluginContext MakeContext(
        string calendarUrl = "http://fake/cal.ics",
        string sectionTitle = "Calendar")
        => new(_eventBus, _ => new CalDavSourceConfig
        {
            Calendars = [new CalDavCalendarConfig { CalendarUrl = calendarUrl }],
            DigestSectionTitle = sectionTitle,
            AlertMinutes = [15],
            PollIntervalMinutes = 15,
            LookaheadHours = 24
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
}
