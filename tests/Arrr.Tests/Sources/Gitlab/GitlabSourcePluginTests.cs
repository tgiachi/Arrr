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

    // ── todos ─────────────────────────────────────────────────────────────────

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
        var ctx = new FakePluginContext(
            _eventBus,
            _ => new GitlabConfig
            {
                Servers = [new GitlabServerConfig { PersonalAccessToken = "" }]
            }
        );

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

    // ── pipelines ─────────────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_Pipeline_DoesNotPublish_OnFirstPoll()
    {
        _handler.ResponsesByUrl["/todos"] = BuildTodosJson([]);
        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson(
            [new(100, "failed", "main", "https://gitlab.com/org/proj/-/pipelines/100")]
        );

        var ctx = MakeContextWithPipelines(["org/proj"]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_Pipeline_PublishesOnTerminalStatus()
    {
        _handler.ResponsesByUrl["/todos"] = BuildTodosJson([]);
        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson([]);

        var ctx = MakeContextWithPipelines(["org/proj"]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson(
            [new(200, "success", "main", "https://gitlab.com/org/proj/-/pipelines/200")]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("✅"));
        Assert.That(n.Title, Does.Contain("org/proj"));
        Assert.That(n.Title, Does.Contain("[main]"));
        Assert.That(n.Body, Does.Contain("200"));
    }

    [Test]
    public async Task PollAsync_Pipeline_DoesNotPublish_NonTerminalStatus()
    {
        _handler.ResponsesByUrl["/todos"] = BuildTodosJson([]);
        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson([]);

        var ctx = MakeContextWithPipelines(["org/proj"]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson(
            [new(300, "running", "develop", "")]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_Pipeline_DoesNotPublish_Duplicates()
    {
        _handler.ResponsesByUrl["/todos"] = BuildTodosJson([]);
        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson([]);

        var ctx = MakeContextWithPipelines(["org/proj"]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson(
            [new(400, "failed", "main", "")]
        );

        await _plugin.PollAsync(ctx, cts.Token); // publishes
        await _plugin.PollAsync(ctx, cts.Token); // same — must NOT publish again

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PollAsync_Pipeline_FailedPipeline_HasHighPriority()
    {
        _handler.ResponsesByUrl["/todos"] = BuildTodosJson([]);
        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson([]);

        var ctx = MakeContextWithPipelines(["org/proj"]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson(
            [new(500, "failed", "main", "")]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Priority, Is.EqualTo(NotificationPriority.High));
        Assert.That(n.Title, Does.Contain("❌"));
    }

    [Test]
    public async Task PollAsync_Pipeline_SetsExtras()
    {
        _handler.ResponsesByUrl["/todos"] = BuildTodosJson([]);
        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson([]);

        var ctx = MakeContextWithPipelines(["org/proj"]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponsesByUrl["/pipelines"] = BuildPipelinesJson(
            [new(600, "canceled", "feature/x", "https://gitlab.com/org/proj/-/pipelines/600")]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.GetExtra("gitlab.project"), Is.EqualTo("org/proj"));
        Assert.That(n.GetExtra("gitlab.pipeline_id"), Is.EqualTo("600"));
        Assert.That(n.GetExtra("gitlab.ref"), Is.EqualTo("feature/x"));
        Assert.That(n.GetExtra("gitlab.status"), Is.EqualTo("canceled"));
        Assert.That(n.Url, Is.EqualTo("https://gitlab.com/org/proj/-/pipelines/600"));
    }

    // ── multi-server ──────────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_MultiServer_PublishesFromBothServers()
    {
        _handler.ResponsesByUrl["gitlab.com/api/v4/todos"] = BuildTodosJson([]);
        _handler.ResponsesByUrl["self-hosted.example.com/api/v4/todos"] = BuildTodosJson([]);

        var ctx = MakeContextMultiServer(
            new GitlabServerConfig { GitlabUrl = "https://gitlab.com", PersonalAccessToken = "tok1" },
            new GitlabServerConfig { GitlabUrl = "https://self-hosted.example.com", PersonalAccessToken = "tok2" }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed both

        _handler.ResponsesByUrl["gitlab.com/api/v4/todos"] = BuildTodosJson(
            [new(1, "Todo on gitlab.com", "Issue", "mentioned", "org/repo", "")]
        );
        _handler.ResponsesByUrl["self-hosted.example.com/api/v4/todos"] = BuildTodosJson(
            [new(1, "Todo on self-hosted", "Issue", "mentioned", "team/repo", "")]
        );

        await _plugin.PollAsync(ctx, cts.Token);

        // Same ID (1) but different servers — both must be published
        Assert.That(_eventBus.Published, Has.Count.EqualTo(2));
        var bodies = _eventBus.Published.Cast<Notification>().Select(n => n.Body).ToList();
        Assert.That(bodies, Has.Some.Contains("Todo on gitlab.com"));
        Assert.That(bodies, Has.Some.Contains("Todo on self-hosted"));
    }

    [Test]
    public async Task PollAsync_MultiServer_SkipsServerWithEmptyToken()
    {
        _handler.ResponseContent = BuildTodosJson([]);

        var ctx = MakeContextMultiServer(
            new GitlabServerConfig { GitlabUrl = "https://gitlab.com", PersonalAccessToken = "tok1" },
            new GitlabServerConfig { GitlabUrl = "https://other.example.com", PersonalAccessToken = "" }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        // Only one server polled (the one with a token)
        var requestUrls = _handler.Requests.Select(r => r.RequestUri?.Host).ToList();
        Assert.That(requestUrls, Has.None.EqualTo("other.example.com"));
        Assert.That(requestUrls, Has.Some.EqualTo("gitlab.com"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

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

    private static string BuildPipelinesJson(
        IEnumerable<(int Id, string Status, string Ref, string WebUrl)> items
    )
    {
        var list = items.Select(
            i => new
            {
                id = i.Id,
                status = i.Status,
                @ref = i.Ref,
                web_url = i.WebUrl,
                updated_at = DateTimeOffset.UtcNow
            }
        );

        return JsonSerializer.Serialize(
            list,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }
        );
    }

    private FakePluginContext MakeContext()
        => new(
            _eventBus,
            _ => new GitlabConfig
            {
                Servers = [new GitlabServerConfig { PersonalAccessToken = "glpat_fake" }]
            }
        );

    private FakePluginContext MakeContextWithPipelines(List<string> projects)
        => new(
            _eventBus,
            _ => new GitlabConfig
            {
                Servers =
                [
                    new GitlabServerConfig
                    {
                        PersonalAccessToken = "glpat_fake",
                        PipelineProjects = projects
                    }
                ]
            }
        );

    private FakePluginContext MakeContextMultiServer(params GitlabServerConfig[] servers)
        => new(
            _eventBus,
            _ => new GitlabConfig { Servers = [..servers] }
        );
}
