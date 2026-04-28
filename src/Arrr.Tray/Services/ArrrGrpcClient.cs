using System.Text.Json;
using Arrr.Tray.Grpc;
using Grpc.Core;
using Grpc.Net.Client;

namespace Arrr.Tray.Services;

public sealed class ArrrGrpcClient : IDisposable
{
    private static readonly HttpClient _http = new();

    private GrpcChannel? _channel;
    private NotificationService.NotificationServiceClient? _client;
    private CancellationTokenSource? _subscribeCts;
    private string? _serverUrl;
    private string? _apiKey;
    private bool _streamWasUp;

    public bool IsConnected { get; private set; }

    public event Action<bool>? DndChanged;
    public event Action<Grpc.NotificationEvent>? NotificationReceived;
    public event Action? SubscriptionConnected;
    public event Action? SubscriptionDisconnected;

    public void Connect(string serverUrl, string apiKey = "", string? grpcUrl = null)
    {
        Dispose();

        _serverUrl = serverUrl;
        _apiKey = apiKey;
        var effectiveGrpcUrl = grpcUrl ?? serverUrl;
        _channel = GrpcChannel.ForAddress(effectiveGrpcUrl);
        _client = new NotificationService.NotificationServiceClient(_channel);
        IsConnected = true;
    }

    private Metadata ApiKeyHeaders() => new() { { "x-api-key", _apiKey ?? "" } };

    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        if (_serverUrl is null)
        {
            return null;
        }

        try
        {
            var json = await _http.GetStringAsync($"{_serverUrl}/api/version", ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("version").GetString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> GetDndAsync(CancellationToken ct = default)
    {
        if (_client is null)
        {
            return false;
        }

        var response = await _client.GetDndAsync(new GetDndRequest(), headers: ApiKeyHeaders(), cancellationToken: ct);

        return response.Enabled;
    }

    public async Task SetDndAsync(bool enabled, CancellationToken ct = default)
    {
        if (_client is null)
        {
            return;
        }

        await _client.SetDndAsync(new SetDndRequest { Enabled = enabled }, headers: ApiKeyHeaders(), cancellationToken: ct);
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
                var call = _client!.Subscribe(
                    new SubscribeRequest(),
                    new CallOptions(headers: ApiKeyHeaders(), cancellationToken: ct));
                _streamWasUp = true;
                SubscriptionConnected?.Invoke();

                while (await call.ResponseStream.MoveNext(ct))
                {
                    var ev = call.ResponseStream.Current;

                    if (ev.PayloadCase == ArrEvent.PayloadOneofCase.Dnd)
                    {
                        DndChanged?.Invoke(ev.Dnd.Enabled);
                    }
                    else if (ev.PayloadCase == ArrEvent.PayloadOneofCase.Notification)
                    {
                        NotificationReceived?.Invoke(ev.Notification);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Only signal disconnected if the stream was previously up
                if (_streamWasUp)
                {
                    _streamWasUp = false;
                    SubscriptionDisconnected?.Invoke();
                }

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
        _apiKey = null;
        _streamWasUp = false;
        IsConnected = false;
    }
}
