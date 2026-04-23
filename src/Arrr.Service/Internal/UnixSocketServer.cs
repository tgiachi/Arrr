using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;

namespace Arrr.Service.Internal;

/// <summary>
/// Listens on a Unix domain socket and broadcasts incoming <see cref="Notification"/> entries
/// as newline-delimited JSON to all connected clients.
/// </summary>
internal class UnixSocketServer : IAsyncDisposable
{
    private readonly ILogger<UnixSocketServer> _logger;
    private readonly string _socketPath;
    private readonly List<NetworkStream> _clients = new();
    private readonly SemaphoreSlim _clientsLock = new(1, 1);

    private Socket? _listener;

    /// <summary>Initializes the server with the socket path.</summary>
    public UnixSocketServer(ILogger<UnixSocketServer> logger, string socketPath)
    {
        _logger = logger;
        _socketPath = socketPath;
    }

    /// <summary>Starts accepting clients until cancellation is requested.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(10);

        _logger.LogInformation("Arrr service started, listening on {Path}", _socketPath);

        await AcceptClientsAsync(ct);
    }

    /// <summary>Broadcasts a notification as JSON to all connected clients.</summary>
    public async Task BroadcastAsync(Notification notification, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(notification) + "\n";
        var bytes = Encoding.UTF8.GetBytes(json);

        await _clientsLock.WaitAsync(ct);
        var dead = new List<NetworkStream>();

        foreach (var client in _clients)
        {
            try { await client.WriteAsync(bytes, ct); }
            catch { dead.Add(client); }
        }

        foreach (var d in dead)
        {
            _clients.Remove(d);
            await d.DisposeAsync();
        }

        _clientsLock.Release();
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listener!.AcceptAsync(ct);
                var stream = new NetworkStream(clientSocket, true);

                await _clientsLock.WaitAsync(ct);
                _clients.Add(stream);
                _clientsLock.Release();

                _logger.LogDebug("Client connected, total: {Count}", _clients.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error accepting client"); }
        }
    }

    /// <summary>Closes all client connections, disposes the listener socket and removes the socket file.</summary>
    public async ValueTask DisposeAsync()
    {
        await _clientsLock.WaitAsync();
        foreach (var client in _clients)
            client.Dispose();
        _clients.Clear();
        _clientsLock.Release();

        _listener?.Dispose();

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
    }
}
