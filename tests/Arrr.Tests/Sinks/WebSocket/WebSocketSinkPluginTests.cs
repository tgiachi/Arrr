using Arrr.Core.Data.Notifications;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Arrr.Tests.Support;
using WebSocketSink;
using WebSocketSink.Data;

namespace Arrr.Tests.Sinks.WebSocket;

[TestFixture]
public class WebSocketSinkPluginTests
{
    private int _port;
    private WebSocketSinkPlugin? _sink;

    [Test]
    public async Task ConsumeAsync_BroadcastsJsonFrame_ToConnectedClient()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _sink!.StartAsync(ctx, cts.Token);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new($"ws://localhost:{_port}/ws"), cts.Token);
        await Task.Delay(100, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        var buffer = new byte[4096];
        var result = await client.ReceiveAsync(buffer, cts.Token);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var received = JsonSerializer.Deserialize<Notification>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(received, Is.EqualTo(notification));
    }

    [Test]
    public async Task ConsumeAsync_DeadClient_DoesNotThrow()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _sink!.StartAsync(ctx, cts.Token);

        var client = new ClientWebSocket();
        await client.ConnectAsync(new($"ws://localhost:{_port}/ws"), cts.Token);
        await Task.Delay(100, cts.Token);

        client.Dispose();
        await Task.Delay(100, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        Assert.DoesNotThrowAsync(() => _sink.ConsumeAsync(notification, cts.Token));
    }

    [SetUp]
    public void SetUp()
    {
        _port = GetFreePort();
        _sink = new();
    }

    [Test]
    public async Task StartAsync_ServerAcceptsWebSocketConnections()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _sink!.StartAsync(ctx, cts.Token);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new($"ws://localhost:{_port}/ws"), cts.Token);

        Assert.That(client.State, Is.EqualTo(WebSocketState.Open));
    }

    [Test]
    public async Task StopAsync_ServerNoLongerAcceptsConnections()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new WebSocketSinkConfig { Port = _port, Path = "ws" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _sink!.StartAsync(ctx, cts.Token);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new($"ws://localhost:{_port}/ws"), cts.Token);
        await Task.Delay(100, cts.Token);

        await _sink.StopAsync();

        using var client2 = new ClientWebSocket();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectEx = Assert.CatchAsync(
            () =>
                client2.ConnectAsync(new($"ws://localhost:{_port}/ws"), cts2.Token)
        );
        Assert.That(connectEx, Is.Not.Null);
    }

    [Test]
    public void StopAsync_WhenNeverStarted_DoesNotThrow()
        => Assert.DoesNotThrowAsync(() => _sink!.StopAsync());

    [TearDown]
    public async Task TearDown()
    {
        if (_sink is not null)
        {
            await _sink.StopAsync();
            _sink.Dispose();
            _sink = null;
        }
    }

    private static int GetFreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();

        return port;
    }
}
