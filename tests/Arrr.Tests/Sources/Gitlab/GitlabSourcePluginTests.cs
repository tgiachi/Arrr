using System.Text.Json;
using Arrr.Plugin.Gitlab;
using Arrr.Plugin.Gitlab.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sources.Gitlab;

[TestFixture]
public class GitlabSourcePluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private FakeEventBus _eventBus = null!;
    private GitlabSourcePlugin? _plugin;

    [Test]
    public async Task PollAsync_DoesNotPublish_OnFirstPoll()
    {
        _handler.ResponseContent = BuildTodosJson(
            [new(1, "Fix crash", "Issue", "mentioned", "owner/repo", "https://gitlab.com/owner/repo/-/issues/1")]
        );

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_DoesNotPublish_Duplicates()
    {
        _handler.ResponseContent = BuildTodosJson(
            [new(2, "Old MR", "MergeRequest", "review_requested", "owner/repo", "")]
        );

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        await _plugin.PollAsync(ctx, cts.Token); // same data
        await _plugin.PollAsync(ctx, cts.Token); // same data again

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_PublishesNewTodo_OnSecondPoll()
    {
        _handler.ResponseContent = BuildTodosJson(
            [new(3, "Existing MR", "MergeRequest", "review_requested", "owner/repo", "")]
        );

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponseContent = BuildTodosJson(
            [
                new(3, "Existing MR", "MergeRequest", "review_requested", "owner/repo", ""),
                new(4, "New security issue", "Issue", "mentioned", "owner/repo", "https://gitlab.com/owner/repo/-/issues/4")
            ]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("owner/repo"));
        Assert.That(n.Body, Does.Contain("New security issue"));
    }

    [Test]
    public async Task PollAsync_TargetTypeMapsToEmoji()
    {
        _handler.ResponseContent = BuildTodosJson([]);
        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed (empty)

        _handler.ResponseContent = BuildTodosJson(
            [
                new(10, "MR title", "MergeRequest", "review_requested", "org/proj", ""),
                new(11, "Epic title", "Epic", "mentioned", "org/proj", ""),
                new(12, "Commit title", "Commit", "directly_addressed", "org/proj", "")
            ]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(3));
        var titles = _eventBus.Published.Cast<Notification>().Select(n => n.Title).ToList();
        Assert.That(titles, Has.Some.Contains("🔀"));
        Assert.That(titles, Has.Some.Contains("🗂️"));
        Assert.That(titles, Has.Some.Contains("📝"));
    }

    [Test]
    public async Task PollAsync_WhenTokenEmpty_DoesNotSend()
    {
        var ctx = new FakePluginContext(_eventBus, _ => new GitlabConfig { PersonalAccessToken = "" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_handler.LastRequest, Is.Null);
    }

    [Test]
    public async Task PollAsync_SetsUrlOnNotification()
    {
        _handler.ResponseContent = BuildTodosJson([]);
        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponseContent = BuildTodosJson(
            [new(20, "Issue with URL", "Issue", "mentioned", "org/proj", "https://gitlab.com/org/proj/-/issues/20")]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Url, Is.EqualTo("https://gitlab.com/org/proj/-/issues/20"));
    }

    [Test]
    public async Task PollAsync_SetsExtras()
    {
        _handler.ResponseContent = BuildTodosJson([]);
        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponseContent = BuildTodosJson(
            [new(30, "Epic todo", "Epic", "assigned", "mygroup/myproject", "")]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.GetExtra("gitlab.project"), Is.EqualTo("mygroup/myproject"));
        Assert.That(n.GetExtra("gitlab.target_type"), Is.EqualTo("Epic"));
        Assert.That(n.GetExtra("gitlab.action"), Is.EqualTo("assigned"));
    }

    [SetUp]
    public void SetUp()
    {
        _handler = new();
        _eventBus = new();
        _plugin = new(_handler);
    }

    [TearDown]
    public void TearDown()
    {
        _plugin?.Dispose();
        _plugin = null;
        _handler.Dispose();
    }

    private static string BuildTodosJson(
        IEnumerable<(int Id, string Title, string TargetType, string ActionName, string Project, string WebUrl)> items
    )
    {
        var list = items.Select(
            i => new
            {
                id = i.Id,
                action_name = i.ActionName,
                target_type = i.TargetType,
                target = new { title = i.Title, web_url = i.WebUrl },
                project = new { path_with_namespace = i.Project },
                created_at = DateTimeOffset.UtcNow
            }
        );

        return JsonSerializer.Serialize(
            list,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }
        );
    }

    private FakePluginContext MakeContext()
        => new(_eventBus, _ => new GitlabConfig { PersonalAccessToken = "glpat_fake" });
}
