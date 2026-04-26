using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Plugin.Github;
using Arrr.Plugin.Github.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sources.Github;

[TestFixture]
public class GithubSourcePluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private FakeEventBus _eventBus = null!;
    private GithubSourcePlugin? _plugin;

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

    [Test]
    public async Task PollAsync_DoesNotPublish_OnFirstPoll()
    {
        _handler.ResponseContent = BuildNotificationsJson([
            new("id-1", "Fix crash", "Issue", "mention", "owner/repo")
        ]);

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_PublishesNewNotification_OnSecondPoll()
    {
        _handler.ResponseContent = BuildNotificationsJson([
            new("id-2", "Add feature", "PullRequest", "review_requested", "owner/repo")
        ]);

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        _handler.ResponseContent = BuildNotificationsJson([
            new("id-2", "Add feature", "PullRequest", "review_requested", "owner/repo"),
            new("id-3", "Security update", "Release", "subscribed", "owner/repo")
        ]);

        await _plugin.PollAsync(ctx, cts.Token); // new item

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("owner/repo"));
        Assert.That(n.Body, Does.Contain("Security update"));
    }

    [Test]
    public async Task PollAsync_DoesNotPublish_Duplicates()
    {
        _handler.ResponseContent = BuildNotificationsJson([
            new("id-4", "Old issue", "Issue", "mention", "owner/repo")
        ]);

        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed

        await _plugin.PollAsync(ctx, cts.Token); // same data
        await _plugin.PollAsync(ctx, cts.Token); // same data again

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_WhenTokenEmpty_DoesNotSend()
    {
        var ctx = new FakePluginContext(_eventBus, _ => new GithubConfig { PersonalAccessToken = "" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_handler.LastRequest, Is.Null);
    }

    [Test]
    public async Task PollAsync_SubjectTypeMapsToEmoji()
    {
        _handler.ResponseContent = BuildNotificationsJson([]);
        var ctx = MakeContext();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _plugin!.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // seed (empty)

        _handler.ResponseContent = BuildNotificationsJson([
            new("e-1", "PR title", "PullRequest", "review_requested", "org/proj"),
            new("e-2", "Discussion title", "Discussion", "comment", "org/proj")
        ]);

        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(2));
        var titles = _eventBus.Published.Cast<Notification>().Select(n => n.Title).ToList();
        Assert.That(titles, Has.Some.Contains("🔀"));
        Assert.That(titles, Has.Some.Contains("💬"));
    }

    private FakePluginContext MakeContext()
        => new(_eventBus, _ => new GithubConfig { PersonalAccessToken = "ghp_fake" });

    private static string BuildNotificationsJson(
        IEnumerable<(string Id, string Title, string Type, string Reason, string Repo)> items)
    {
        var list = items.Select(i => new
        {
            id = i.Id,
            subject = new { title = i.Title, type = i.Type, url = "" },
            reason = i.Reason,
            repository = new { full_name = i.Repo },
            updated_at = DateTimeOffset.UtcNow
        });

        return JsonSerializer.Serialize(list, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }
}
