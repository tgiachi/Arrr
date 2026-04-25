using System.Net;
using System.Net.Sockets;
using Arrr.Core.Data.Notifications;
using Arrr.Tests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using SignalRSink;
using SignalRSink.Data;

namespace Arrr.Tests.Sinks.SignalR;

[TestFixture]
public class SignalRSinkPluginTests
{
    private int _port;
    private SignalRSinkPlugin? _sink;

    [SetUp]
    public void SetUp()
    {
        _port = GetFreePort();
        _sink = new SignalRSinkPlugin();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_sink is not null)
        {
            await _sink.StopAsync();
            _sink = null;
        }
    }

    [Test]
    public async Task StartAsync_HubAcceptsSignalRConnections()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new SignalRSinkConfig { Port = _port, HubPath = "notifications" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _sink!.StartAsync(ctx, cts.Token);

        await using var conn = new HubConnectionBuilder()
            .WithUrl($"http://localhost:{_port}/notifications")
            .Build();

        await conn.StartAsync(cts.Token);

        Assert.That(conn.State, Is.EqualTo(HubConnectionState.Connected));

        await conn.StopAsync();
    }

    [Test]
    public async Task ConsumeAsync_ClientReceivesNotification()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new SignalRSinkConfig { Port = _port, HubPath = "notifications" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _sink!.StartAsync(ctx, cts.Token);

        var tcs = new TaskCompletionSource<Notification>();

        await using var conn = new HubConnectionBuilder()
            .WithUrl($"http://localhost:{_port}/notifications")
            .Build();

        conn.On<Notification>("ReceiveNotification", n => tcs.TrySetResult(n));
        await conn.StartAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(received, Is.EqualTo(notification));

        await conn.StopAsync();
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
