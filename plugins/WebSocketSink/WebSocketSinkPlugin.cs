using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebSocketSink.Data;

namespace WebSocketSink;

public class WebSocketSinkPlugin : ISinkPlugin, IConfigurablePlugin, IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();

    private WebApplication? _app;
    private ISinkContext? _context;
    private CancellationTokenSource _stopCts = new();

    public string Id          => "com.arrr.sink.websocket";
    public string Name        => "WebSocket";
    public string Version     => "1.0.0";
    public string Author      => "Arrr";
    public string Description => "Broadcasts notifications as JSON frames to connected WebSocket clients.";
    public string Icon        => "🔗";
    public Type   ConfigType  => typeof(WebSocketSinkConfig);

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        var config = await context.LoadConfigAsync<WebSocketSinkConfig>(ct);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = "WebSocketSink" });
        builder.WebHost.UseSetting("urls", $"http://0.0.0.0:{config.Port}");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.UseWebSockets();

        var path = "/" + config.Path.TrimStart('/');

        _app.Map(path, async (HttpContext httpContext) =>
        {
            if (!httpContext.WebSockets.IsWebSocketRequest)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var ws = await httpContext.WebSockets.AcceptWebSocketAsync();
            var id = Guid.NewGuid();
            _clients.TryAdd(id, ws);
            _context?.Logger.LogDebug("WebSocket client connected ({Id}), total: {Count}", id, _clients.Count);

            var buffer = new byte[64];
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(buffer, _stopCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _context?.Logger.LogDebug(ex, "WebSocket receive error for client {Id}, closing connection", id);
                    break;
                }
            }

            _clients.TryRemove(id, out _);
        });

        await _app.StartAsync(ct);
        context.Logger.LogInformation("WebSocket sink listening on ws://0.0.0.0:{Port}/{Path}", config.Port, config.Path);
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_clients.IsEmpty)
            return;

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification, _jsonOptions));
        var segment = new ArraySegment<byte>(bytes);
        var dead = new List<Guid>();

        foreach (var (id, ws) in _clients)
        {
            if (ws.State != WebSocketState.Open)
            {
                dead.Add(id);
                continue;
            }

            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                dead.Add(id);
            }
        }

        foreach (var id in dead)
            _clients.TryRemove(id, out _);
    }

    public async Task StopAsync()
    {
        _stopCts.Cancel();

        foreach (var ws in _clients.Values)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "server stopping", CancellationToken.None);
            }
            catch { /* client already gone */ }
        }

        _clients.Clear();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        _stopCts.Dispose();
        _stopCts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _stopCts.Dispose();
    }
}
