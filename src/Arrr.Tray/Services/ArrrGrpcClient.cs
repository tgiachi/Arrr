using Arrr.Tray.Grpc;
using Grpc.Core;
using Grpc.Net.Client;

namespace Arrr.Tray.Services;

public sealed class ArrrGrpcClient : IDisposable
{
    private GrpcChannel? _channel;
    private NotificationService.NotificationServiceClient? _client;
    private CancellationTokenSource? _subscribeCts;

    public bool IsConnected { get; private set; }

    public event Action<bool>? DndChanged;

    public void Connect(string serverUrl)
    {
        Dispose();

        _channel = GrpcChannel.ForAddress(serverUrl);
        _client = new NotificationService.NotificationServiceClient(_channel);
        IsConnected = true;
    }

    public async Task<bool> GetDndAsync(CancellationToken ct = default)
    {
        if (_client is null)
        {
            return false;
        }

        var response = await _client.GetDndAsync(new GetDndRequest(), cancellationToken: ct);

        return response.Enabled;
    }

    public async Task SetDndAsync(bool enabled, CancellationToken ct = default)
    {
        if (_client is null)
        {
            return;
        }

        await _client.SetDndAsync(new SetDndRequest { Enabled = enabled }, cancellationToken: ct);
    }

    public void StartSubscription()
    {
        if (_client is null)
        {
            return;
        }

        _subscribeCts?.Cancel();
        _subscribeCts = new CancellationTokenSource();
        _ = Task.Run(() => SubscribeLoopAsync(_subscribeCts.Token));
    }

    private async Task SubscribeLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var call = _client!.Subscribe(new SubscribeRequest(), cancellationToken: ct);

                while (await call.ResponseStream.MoveNext(ct))
                {
                    var ev = call.ResponseStream.Current;

                    if (ev.PayloadCase == ArrEvent.PayloadOneofCase.Dnd)
                    {
                        DndChanged?.Invoke(ev.Dnd.Enabled);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    public void Dispose()
    {
        _subscribeCts?.Cancel();
        _subscribeCts?.Dispose();
        _subscribeCts = null;
        _channel?.Dispose();
        _channel = null;
        _client = null;
        IsConnected = false;
    }
}
