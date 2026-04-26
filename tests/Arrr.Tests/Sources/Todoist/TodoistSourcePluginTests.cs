using System.Net;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Types;
using Arrr.Plugin.Todoist;
using Arrr.Plugin.Todoist.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sources.Todoist;

[TestFixture]
public class TodoistSourcePluginTests
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

    // ── empty token ──────────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_EmptyApiToken_ReturnsEarly()
    {
        var ctx = MakeContext(_eventBus, token: "");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_httpHandler.RequestedUrls, Is.Empty);
        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── due date alerts ──────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_TaskDueInAlertWindow_PublishesNotification()
    {
        var now = _timeProvider.GetUtcNow();
        var dueTime = now.AddMinutes(20);
        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, TasksJson("t-1", "Daily standup", 1, dueTime));

        var ctx = MakeContext(_eventBus);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("Daily standup"));
        Assert.That(n.Body, Does.Contain("15"));
    }

    [Test]
    public async Task PollAsync_TaskAlreadyNotified_DoesNotDuplicate()
    {
        var now = _timeProvider.GetUtcNow();
        var dueTime = now.AddMinutes(20);
        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, TasksJson("t-1", "Standup", 1, dueTime));

        var ctx = MakeContext(_eventBus);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PollAsync_FirstPoll_SkipsPastTriggers()
    {
        var now = _timeProvider.GetUtcNow();
        var dueTime = now.AddMinutes(5);
        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, TasksJson("t-1", "Old task", 1, dueTime));

        var ctx = MakeContext(_eventBus);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_TaskWithDateOnly_Skipped()
    {
        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, TasksJson("t-1", "All day task", 1, null, "2026-04-26"));

        var ctx = MakeContext(_eventBus);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_FilterEncodedInUrl()
    {
        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, "[]");

        var ctx = MakeContext(_eventBus, filter: "today | overdue");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_httpHandler.RequestedUrls, Has.Some.Contains("today%20%7C%20overdue"));
    }

    [Test]
    public async Task PollAsync_CriticalPriority_MapsCorrectly()
    {
        var now = _timeProvider.GetUtcNow();
        var dueTime = now.AddMinutes(20);
        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, TasksJson("t-1", "Critical task", 4, dueTime));

        var ctx = MakeContext(_eventBus);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Priority, Is.EqualTo(NotificationPriority.Critical));
    }

    // ── reminders ────────────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_ReminderPublished()
    {
        var now = _timeProvider.GetUtcNow();
        var reminderTime = now.AddMinutes(5);
        var farFuture = now.AddMinutes(200);

        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, TasksJson("t-1", "Buy milk", 1, farFuture));
        _httpHandler.SetRoute("reminders", HttpStatusCode.OK, RemindersJson("r-1", "t-1", reminderTime));

        var ctx = MakeContext(_eventBus, notifyReminders: true);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.StartWith("⏰"));
        Assert.That(n.Title, Does.Contain("Buy milk"));
    }

    [Test]
    public async Task PollAsync_Reminders403_DisablesReminderFetch()
    {
        _httpHandler.SetRoute("tasks", HttpStatusCode.OK, "[]");
        _httpHandler.SetRoute("reminders", HttpStatusCode.Forbidden, "");

        var ctx = MakeContext(_eventBus, notifyReminders: true);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await _plugin.PollAsync(ctx, cts.Token);

        var reminderCalls = _httpHandler.RequestedUrls.Count(u => u.Contains("reminders"));
        Assert.That(reminderCalls, Is.EqualTo(1));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static FakePluginContext MakeContext(
        FakeEventBus bus,
        string token = "test-token",
        string filter = "today | overdue",
        List<int>? alertMinutes = null,
        bool notifyReminders = false)
        => new(bus, _ => new TodoistConfig
        {
            ApiToken = token,
            Filter = filter,
            AlertMinutes = alertMinutes ?? [15],
            PollIntervalMinutes = 5,
            NotifyReminders = notifyReminders
        });

    private static string TasksJson(
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

        return $"[{{\"id\":\"{id}\",\"content\":\"{content}\",\"priority\":{priority},\"project_id\":\"proj-1\",\"due\":{due}}}]";
    }

    private static string RemindersJson(string id, string taskId, DateTimeOffset dueTime)
        => $"[{{\"id\":\"{id}\",\"task_id\":\"{taskId}\",\"due\":{{\"date\":\"{dueTime:yyyy-MM-dd}\",\"datetime\":\"{dueTime:yyyy-MM-ddTHH:mm:ssZ}\"}}}}]";

    private sealed class TodoistFakeHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode code, string content)> _routes = new();
        public List<string> RequestedUrls { get; } = [];

        public void SetRoute(string urlSubstring, HttpStatusCode code, string content)
            => _routes[urlSubstring] = (code, content);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri?.ToString() ?? "";
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
