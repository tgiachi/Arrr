using Arrr.Sink.MacNotify;
using Arrr.Sink.MacNotify.Data;

namespace Arrr.Tests.Sinks.MacNotify;

[TestFixture]
public class MacNotifySinkPluginTests
{
    [Test]
    public async Task ConsumeAsync_WhenNotStarted_DoesNotThrow()
    {
        var plugin = new MacNotifySinkPlugin();
        var notification = new Notification(Guid.NewGuid(), "src", "Title", "Body", DateTimeOffset.UtcNow, null);
        Assert.DoesNotThrowAsync(() => plugin.ConsumeAsync(notification, CancellationToken.None));
        await Task.CompletedTask;
    }

    [Test]
    public void Metadata_IsCorrect()
    {
        var plugin = new MacNotifySinkPlugin();
        Assert.Multiple(
            () =>
            {
                Assert.That(plugin.Id, Is.EqualTo("com.arrr.sink.mac-notify"));
                Assert.That(plugin.Platforms, Is.EquivalentTo(new[] { PlatformType.Osx }));
                Assert.That(plugin.ConfigType, Is.EqualTo(typeof(MacNotifyConfig)));
            }
        );
    }

    [Test]
    public async Task StopAsync_DoesNotThrow()
    {
        var plugin = new MacNotifySinkPlugin();
        Assert.DoesNotThrowAsync(() => plugin.StopAsync());
        await Task.CompletedTask;
    }
}
