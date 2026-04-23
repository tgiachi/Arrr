using System.Threading.Channels;
using Arrr.Core.Data.Notifications;
using Arrr.Service.Internal;
using Arrr.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Service;

[TestFixture]
public class PluginRunnerTests
{
    [Test]
    public async Task StartAll_WhenPluginWritesNotification_NotificationAppearsInChannel()
    {
        var expected = new Notification(Guid.NewGuid(), "fake", "Title", "Body", DateTimeOffset.UtcNow, null);
        var plugin = new FakeSourcePlugin("fake", [expected]);
        var channel = Channel.CreateUnbounded<Notification>();
        var runner = new PluginRunner(NullLogger<PluginRunner>.Instance, [plugin], channel.Writer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        runner.StartAll(cts.Token);

        var received = await channel.Reader.ReadAsync(cts.Token);

        Assert.That(received, Is.EqualTo(expected));
    }

    [Test]
    public async Task StartAll_WhenPluginThrows_DoesNotPropagateException()
    {
        var plugin = new FakeSourcePlugin("broken", throws: new InvalidOperationException("boom"));
        var channel = Channel.CreateUnbounded<Notification>();
        var runner = new PluginRunner(NullLogger<PluginRunner>.Instance, [plugin], channel.Writer);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        runner.StartAll(cts.Token);

        await Task.Delay(100);

        Assert.Pass("No exception propagated from crashing plugin.");
    }
}
