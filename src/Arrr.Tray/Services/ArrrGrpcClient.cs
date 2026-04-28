using System.Text.Json;
using System.Text.Json.Serialization;
using Arrr.Tray.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Serilog;

namespace Arrr.Tray.Services;

internal sealed class IconBundle
{
    [JsonPropertyName("plugins")]
    public Dictionary<string, string>? Plugins { get; set; }

    [JsonPropertyName("sinks")]
    public Dictionary<string, string>? Sinks { get; set; }
}

public sealed class ArrrGrpcClient : IDisposable
{
    private static readonly HttpClient _http = new();
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private GrpcChannel? _channel;
    private NotificationService.NotificationServiceClient? _client;
    private CancellationTokenSource? _subscribeCts;
    private string? _serverUrl;
    private string? _grpcUrl;
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
        _grpcUrl = grpcUrl ?? serverUrl;
        var effectiveGrpcUrl = _grpcUrl;
        _channel = GrpcChannel.ForAddress(effectiveGrpcUrl);
        _client = new NotificationService.NotificationServiceClient(_channel);
        IsConnected = true;
    }

    private Metadata ApiKeyHeaders() => new() { { "x-api-key", _apiKey ?? "" } };

    public async Task<IReadOnlyDictionary<string, byte[]>> GetIconBundleAsync(CancellationToken ct = default)
    {
        if (_serverUrl is null)
            return new Dictionary<string, byte[]>();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_serverUrl}/api/icons");
            request.Headers.Add("x-api-key", _apiKey ?? "");
            var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, byte[]>();

            var json = await response.Content.ReadAsStringAsync(ct);
            var bundle = JsonSerializer.Deserialize<IconBundle>(json, _jsonOpts);

            var result = new Dictionary<string, byte[]>();

            if (bundle?.Plugins is not null)
                foreach (var (id, b64) in bundle.Plugins)
                    result[id] = Convert.FromBase64String(b64);

            if (bundle?.Sinks is not null)
                foreach (var (id, b64) in bundle.Sinks)
                    result[id] = Convert.FromBase64String(b64);

            Log.Debug("Icon bundle fetched: {Count} icons", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to fetch icon bundle");
            return new Dictionary<string, byte[]>();
        }
    }

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

                // Signal connected only after the first successful read so that
                // connection-refused failures don't produce a spurious Connected event.
                var firstRead = true;
                while (await call.ResponseStream.MoveNext(ct))
                {
                    if (firstRead)
                    {
                        firstRead = false;
                        _streamWasUp = true;
                        SubscriptionConnected?.Invoke();
                        Log.Information("gRPC subscription connected to {Url}", _grpcUrl);
                    }

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
            catch (Exception ex)
            {
                Log.Warning(ex, "gRPC subscription error — streamWasUp={WasUp}", _streamWasUp);

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
        _grpcUrl = null;
        _streamWasUp = false;
        IsConnected = false;
    }
}
