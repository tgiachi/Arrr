using Arrr.Core.Data.Notifications;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arrr.Service.Sinks;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sinks;

[TestFixture]
public class UnixSocketSinkTests
{
    private string _socketPath = "";

    [Test]
    public async Task ConsumeAsync_BroadcastsJsonLine_ToConnectedClient()
    {
        var sink = new UnixSocketSink();
        var ctx = new FakeSinkContext(configFactory: _ => new UnixSocketSinkConfig { SocketPath = _socketPath });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sink.StartAsync(ctx, cts.Token);

        await WaitForSocketAsync(_socketPath, cts.Token);

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), cts.Token);
        using var stream = new NetworkStream(client, false);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await sink.ConsumeAsync(notification, cts.Token);

        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, cts.Token);
        var line = Encoding.UTF8.GetString(buffer, 0, read).TrimEnd('\n');
        var received = JsonSerializer.Deserialize<Notification>(line);

        Assert.That(received, Is.EqualTo(notification));

        await sink.StopAsync();
    }

    [SetUp]
    public void SetUp()
        => _socketPath = Path.Combine(Path.GetTempPath(), $"arrr_sink_test_{Guid.NewGuid()}.sock");

    [Test]
    public async Task StartAsync_CreatesSocketFile()
    {
        var sink = new UnixSocketSink();
        var ctx = new FakeSinkContext(configFactory: _ => new UnixSocketSinkConfig { SocketPath = _socketPath });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sink.StartAsync(ctx, cts.Token);

        Assert.That(File.Exists(_socketPath), Is.True);

        await sink.StopAsync();
    }

    [Test]
    public async Task StopAsync_RemovesSocketFile()
    {
        var sink = new UnixSocketSink();
        var ctx = new FakeSinkContext(configFactory: _ => new UnixSocketSinkConfig { SocketPath = _socketPath });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sink.StartAsync(ctx, cts.Token);
        await sink.StopAsync();

        Assert.That(File.Exists(_socketPath), Is.False);
    }

    [Test]
    public void StopAsync_WhenNeverStarted_DoesNotThrow()
    {
        var sink = new UnixSocketSink();
        Assert.DoesNotThrowAsync(() => sink.StopAsync());
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }
    }

    private static async Task WaitForSocketAsync(string path, CancellationToken ct)
    {
        while (!File.Exists(path))
        {
            await Task.Delay(20, ct);
        }
    }
}
