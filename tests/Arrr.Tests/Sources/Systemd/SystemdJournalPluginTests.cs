using Arrr.Core.Data.Notifications;
using Arrr.Plugin.Systemd;
using Arrr.Plugin.Systemd.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sources.Systemd;

[TestFixture]
public class SystemdJournalPluginTests
{
    private FakeEventBus _eventBus = null!;

    [SetUp]
    public void SetUp()
    {
        _eventBus = new();
    }

    [Test]
    public async Task StartAsync_PublishesNotification_ForEachJournalLine()
    {
        var lines = new[]
        {
            """{"MESSAGE":"Out of memory","_SYSTEMD_UNIT":"myapp.service","PRIORITY":"3","__REALTIME_TIMESTAMP":"1700000000000000"}""",
            """{"MESSAGE":"Segfault detected","SYSLOG_IDENTIFIER":"nginx","PRIORITY":"2","__REALTIME_TIMESTAMP":"1700000001000000"}"""
        };

        var plugin = MakePlugin(lines);
        var ctx = new FakePluginContext(_eventBus, _ => new SystemdConfig());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await plugin.StartAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task StartAsync_TitleContainsPriorityLabel_AndUnit()
    {
        var lines = new[]
        {
            """{"MESSAGE":"Disk full","_SYSTEMD_UNIT":"cron.service","PRIORITY":"3","__REALTIME_TIMESTAMP":"1700000000000000"}"""
        };

        var plugin = MakePlugin(lines);
        var ctx = new FakePluginContext(_eventBus, _ => new SystemdConfig());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await plugin.StartAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("ERR"));
        Assert.That(n.Title, Does.Contain("cron.service"));
        Assert.That(n.Body, Is.EqualTo("Disk full"));
    }

    [Test]
    public async Task StartAsync_TruncatesLongMessages_WhenMaxLengthConfigured()
    {
        var longMessage = new string('x', 600);
        var lines = new[]
        {
            $$"""{"MESSAGE":"{{longMessage}}","_SYSTEMD_UNIT":"test.service","PRIORITY":"3"}"""
        };

        var plugin = MakePlugin(lines);
        var ctx = new FakePluginContext(
            _eventBus,
            _ => new SystemdConfig { MaxMessageLength = 100 }
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await plugin.StartAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Body.Length, Is.LessThanOrEqualTo(102)); // 100 chars + "…"
        Assert.That(n.Body, Does.EndWith("…"));
    }

    [Test]
    public async Task StartAsync_FallsBackToSyslogIdentifier_WhenUnitMissing()
    {
        var lines = new[]
        {
            """{"MESSAGE":"Connection refused","SYSLOG_IDENTIFIER":"sshd","PRIORITY":"4"}"""
        };

        var plugin = MakePlugin(lines);
        var ctx = new FakePluginContext(_eventBus, _ => new SystemdConfig());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await plugin.StartAsync(ctx, cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("sshd"));
    }

    [Test]
    public async Task StartAsync_SkipsInvalidJsonLines()
    {
        var lines = new[]
        {
            "not json at all",
            """{"MESSAGE":"Valid entry","_SYSTEMD_UNIT":"ok.service","PRIORITY":"3"}"""
        };

        var plugin = MakePlugin(lines);
        var ctx = new FakePluginContext(_eventBus, _ => new SystemdConfig());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await plugin.StartAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
    }

    private SystemdJournalPlugin MakePlugin(string[] lines)
    {
        return new SystemdJournalPlugin((_, ct) => ToAsyncEnumerable(lines, ct));
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(
        string[] lines,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            yield return line;
            await Task.Yield();
        }
    }
}
