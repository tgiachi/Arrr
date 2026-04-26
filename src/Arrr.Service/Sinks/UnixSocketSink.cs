using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;

namespace Arrr.Service.Sinks;

public class UnixSocketSinkConfig
{
    [Description("Unix domain socket path for streaming notifications as newline-delimited JSON")]
    public string SocketPath { get; set; } = "/tmp/arrr.sock";
}

internal class UnixSocketSink : ISinkPlugin, IConfigurablePlugin
{
    private readonly List<NetworkStream> _clients = [];
    private readonly SemaphoreSlim _clientsLock = new(1, 1);

    private Socket? _listener;
    private ISinkContext? _context;
    private string _socketPath = "/tmp/arrr.sock";
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptTask;

    public string Id => "com.arrr.sink.socket";
    public string Name => "Unix Socket";
    public string Version => "1.0.0";
    public string Author => "Arrr";
    public string Description => "Broadcasts notifications as newline-delimited JSON on a Unix domain socket.";
    public string Icon => "🔌";
    public PlatformType[] Platforms => [PlatformType.Linux, PlatformType.Osx];
    public Type ConfigType => typeof(UnixSocketSinkConfig);

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification) + "\n");

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

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        var config = await context.LoadConfigAsync<UnixSocketSinkConfig>(ct);
        _socketPath = config.SocketPath;

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }

        _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(10);

        context.Logger.LogInformation("Unix socket sink listening on {Path}", _socketPath);

        _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _acceptTask = AcceptClientsAsync(_acceptCts.Token);
    }

    public async Task StopAsync()
    {
        if (_acceptCts is not null)
        {
            await _acceptCts.CancelAsync();

            if (_acceptTask is not null)
            {
                await _acceptTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }

        await _clientsLock.WaitAsync();

        foreach (var client in _clients)
        {
            client.Dispose();
        }
        _clients.Clear();
        _clientsLock.Release();

        _listener?.Dispose();
        _listener = null;

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }
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

                _context?.Logger.LogDebug("Client connected, total: {Count}", _clients.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _context?.Logger.LogError(ex, "Error accepting client"); }
        }
    }
}
