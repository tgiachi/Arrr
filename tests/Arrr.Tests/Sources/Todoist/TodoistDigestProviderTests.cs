using System.Net;
using Arrr.Plugin.Todoist;
using Arrr.Plugin.Todoist.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sources.Todoist;

[TestFixture]
public class TodoistDigestProviderTests
{
    private TodoistFakeHttpHandler _httpHandler = null!;
    private FakeEventBus _eventBus = null!;
    private FakeTimeProvider _timeProvider = null!;
    private TodoistSourcePlugin? _plugin;

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

    // ── empty token → empty section ───────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_EmptyToken_ReturnsEmptySection()
    {
        var ctx = MakeContext(token: "");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Is.Empty);
        Assert.That(_httpHandler.RequestedUrls, Is.Empty);
    }

    // ── timed task today ──────────────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_TimedTaskToday_AppearsWithTime()
    {
        var localNow = _timeProvider.GetLocalNow();
        var dueTime = localNow.AddHours(2); // 08:00 local
        _httpHandler.SetRoute("filter=due%3A", HttpStatusCode.OK,
            TasksJson("t-1", "Team standup", 1, dueTime));

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var today = DateOnly.FromDateTime(localNow.DateTime);
        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Has.Count.EqualTo(1));
        Assert.That(section.Items[0].Text, Does.Contain("Team standup"));
        Assert.That(section.Items[0].Text, Does.Contain(":"));
    }

    // ── date-only task → no time prefix ──────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_DateOnlyTask_AppearsWithoutTime()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        _httpHandler.SetRoute("filter=due%3A", HttpStatusCode.OK,
            TasksJson("t-2", "Write report", 1, null, today.ToString("yyyy-MM-dd")));

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Has.Count.EqualTo(1));
        Assert.That(section.Items[0].Text, Is.EqualTo("Write report"));
    }

    // ── multiple tasks → all items present ───────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_MultipleTasks_AllInSection()
    {
        var localNow = _timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(localNow.DateTime);
        _httpHandler.SetRoute("filter=due%3A", HttpStatusCode.OK,
            $"[{RawTaskJson("t-1", "Standup", 1, localNow.AddHours(2))},{RawTaskJson("t-2", "Review PR", 2, localNow.AddHours(4))}]");

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Has.Count.EqualTo(2));
        Assert.That(section.Items.Select(i => i.Text), Has.Some.Contains("Standup"));
        Assert.That(section.Items.Select(i => i.Text), Has.Some.Contains("Review PR"));
    }

    // ── section title from config ─────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_SectionTitleFromConfig()
    {
        var ctx = MakeContext(sectionTitle: "Today's Tasks");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        Assert.That(_plugin.DigestSectionTitle, Is.EqualTo("Today's Tasks"));
    }

    // ── uses date-specific filter in URL ──────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_UsesDateFilterInRequest()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        _httpHandler.SetRoute("filter=due%3A", HttpStatusCode.OK, "[]");

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        await _plugin.GetDigestSectionAsync(today, cts.Token);

        var expectedFragment = $"due%3A%20{today:yyyy-MM-dd}";
        Assert.That(_httpHandler.RequestedUrls, Has.Some.Contains(expectedFragment));
    }

    // ── HTTP error → empty section ────────────────────────────────────────────

    [Test]
    public async Task GetDigestSectionAsync_HttpError_ReturnsEmptySection()
    {
        _httpHandler.SetRoute("filter=due%3A", HttpStatusCode.InternalServerError, "");

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var section = await _plugin.GetDigestSectionAsync(today, cts.Token);

        Assert.That(section.Items, Is.Empty);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private FakePluginContext MakeContext(
        string token = "fake-token",
        string sectionTitle = "Tasks")
        => new(_eventBus, _ => new TodoistConfig
        {
            ApiToken = token,
            Filter = "today | overdue",
            AlertMinutes = [15],
            PollIntervalMinutes = 5,
            NotifyReminders = false,
            DigestSectionTitle = sectionTitle
        });

    private static string TasksJson(
        string id,
        string content,
        int priority,
        DateTimeOffset? dueDateTime,
        string? dueDateOnly = null)
        => $"[{RawTaskJson(id, content, priority, dueDateTime, dueDateOnly)}]";

    private static string RawTaskJson(
        string id,
        string content,
        int priority,
        DateTimeOffset? dueDateTime,
        string? dueDateOnly = null)
    {
        string due;
        if (dueDateTime is not null)
        {
            due = $"{{\"date\":\"{dueDateTime:yyyy-MM-dd}\",\"datetime\":\"{dueDateTime:yyyy-MM-ddTHH:mm:ssZ}\"}}";
        }
        else if (dueDateOnly is not null)
        {
            due = $"{{\"date\":\"{dueDateOnly}\",\"datetime\":null}}";
        }
        else
        {
            due = "null";
        }

        return $"{{\"id\":\"{id}\",\"content\":\"{content}\",\"priority\":{priority},\"project_id\":\"proj-1\",\"due\":{due}}}";
    }

    private sealed class TodoistFakeHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode code, string content)> _routes = new();
        public List<string> RequestedUrls { get; } = [];

        public void SetRoute(string urlSubstring, HttpStatusCode code, string content)
            => _routes[urlSubstring] = (code, content);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri?.AbsoluteUri ?? "";
            RequestedUrls.Add(url);

            foreach (var (key, (code, content)) in _routes)
            {
                if (url.Contains(key))
                {
                    return Task.FromResult(new HttpResponseMessage(code)
                    {
                        Content = new StringContent(content)
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }
}
