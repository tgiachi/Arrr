using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Tests.Support;
using WebSocketSink;
using WebSocketSink.Data;

namespace Arrr.Tests.Sinks.WebSocket;

[TestFixture]
public class WebSocketSinkPluginTests
{
    private int _port;

    [SetUp]
    public void SetUp()
    {
        _port = GetFreePort();
    }

    [Test]
    public async Task StartAsync_ServerAcceptsWebSocketConnections()
    {
        var sink = new WebSocketSinkPlugin();
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sink.StartAsync(ctx, cts.Token);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://localhost:{_port}/ws"), cts.Token);

        Assert.That(client.State, Is.EqualTo(WebSocketState.Open));

        await sink.StopAsync();
    }

    [Test]
    public async Task ConsumeAsync_BroadcastsJsonFrame_ToConnectedClient()
    {
        var sink = new WebSocketSinkPlugin();
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sink.StartAsync(ctx, cts.Token);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://localhost:{_port}/ws"), cts.Token);
        await Task.Delay(100, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await sink.ConsumeAsync(notification, cts.Token);

        var buffer = new byte[4096];
        var result = await client.ReceiveAsync(buffer, cts.Token);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var received = JsonSerializer.Deserialize<Notification>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(received, Is.EqualTo(notification));

        await sink.StopAsync();
    }

    [Test]
    public async Task ConsumeAsync_DeadClient_DoesNotThrow()
    {
        var sink = new WebSocketSinkPlugin();
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sink.StartAsync(ctx, cts.Token);

        var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://localhost:{_port}/ws"), cts.Token);
        await Task.Delay(100, cts.Token);

        client.Dispose();
        await Task.Delay(100, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        Assert.DoesNotThrowAsync(() => sink.ConsumeAsync(notification, cts.Token));

        await sink.StopAsync();
    }

    [Test]
    public async Task StopAsync_ServerNoLongerAcceptsConnections()
    {
        var sink = new WebSocketSinkPlugin();
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sink.StartAsync(ctx, cts.Token);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://localhost:{_port}/ws"), cts.Token);
        await Task.Delay(100, cts.Token);

        await sink.StopAsync();

        using var client2 = new ClientWebSocket();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectEx = Assert.CatchAsync(() =>
            client2.ConnectAsync(new Uri($"ws://localhost:{_port}/ws"), cts2.Token));
        Assert.That(connectEx, Is.Not.Null);
    }

    [Test]
    public void StopAsync_WhenNeverStarted_DoesNotThrow()
    {
        var sink = new WebSocketSinkPlugin();
        Assert.DoesNotThrowAsync(() => sink.StopAsync());
    }

    private static int GetFreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
