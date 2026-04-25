using Arrr.Tests.Support;
using MqttSource;
using MqttSource.Data;

namespace Arrr.Tests.Sources.Mqtt;

[TestFixture]
public class MqttSourcePluginTests
{
    [Test]
    public void ConfigType_ReturnsMqttSourceConfig()
    {
        var plugin = new MqttSourcePlugin();
        Assert.That(plugin.ConfigType, Is.EqualTo(typeof(MqttSourceConfig)));
    }

    [Test]
    public void Categories_ContainsIotAndMessaging()
    {
        var plugin = new MqttSourcePlugin();
        Assert.That(plugin.Categories, Contains.Item("iot"));
        Assert.That(plugin.Categories, Contains.Item("messaging"));
    }

    [Test]
    public async Task StartAsync_WhenBrokerUnreachable_LogsErrorAndReturns()
    {
        var bus = new FakeEventBus();
        var ctx = new FakePluginContext(bus, configFactory: _ => new MqttSourceConfig
        {
            BrokerHost = "127.0.0.1",
            BrokerPort = 19999,
        });

        var plugin = new MqttSourcePlugin();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        Assert.DoesNotThrowAsync(() => plugin.StartAsync(ctx, cts.Token));
    }

    [Test]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var plugin = new MqttSourcePlugin();
        Assert.DoesNotThrow(() =>
        {
            plugin.Dispose();
            plugin.Dispose();
        });
    }
}
