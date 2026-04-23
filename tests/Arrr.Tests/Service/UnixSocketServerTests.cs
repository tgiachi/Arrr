using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Service.Internal;

namespace Arrr.Tests.Service;

[TestFixture]
public class UnixSocketServerTests
{
    private string _socketPath = "";

    [Test]
    public async Task RunAsync_WhenBroadcastCalled_ClientReceivesJsonLine()
    {
        await using var server = new UnixSocketServer(_socketPath);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = server.RunAsync(cts.Token);

        await WaitForSocketAsync(_socketPath, cts.Token);

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), cts.Token);
        using var stream = new NetworkStream(client, false);

        var notification = new Notification(Guid.NewGuid(), "test", "Hello", "World", DateTimeOffset.UtcNow, null);
        await server.BroadcastAsync(notification, cts.Token);

        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, cts.Token);
        var line = Encoding.UTF8.GetString(buffer, 0, read).TrimEnd('\n');
        var received = JsonSerializer.Deserialize<Notification>(line);

        Assert.That(received, Is.EqualTo(notification));

        cts.Cancel();

        try
        {
            await serverTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    [SetUp]
    public void SetUp()
        => _socketPath = Path.Combine(Path.GetTempPath(), $"arrr_test_{Guid.NewGuid()}.sock");

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
