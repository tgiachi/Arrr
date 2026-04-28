using System.Text.Json;
using System.Text.Json.Serialization;
using Arrr.Tray.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;

namespace Arrr.Tray.Services;

internal sealed class IconBundle
{
    [JsonPropertyName("plugins")]
    public Dictionary<string, string>? Plugins { get; set; }

    [JsonPropertyName("sinks")]
    public Dictionary<string, string>? Sinks { get; set; }
}

public sealed class ArrrServiceClient : IAsyncDisposable
{
    private static readonly HttpClient _http = new();
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private HubConnection? _hub;
    private CancellationTokenSource? _connectCts;
    private string? _serverUrl;
    private string? _apiKey;

    public bool IsConnected { get; private set; }

    public event Action<bool>? DndChanged;
    public event Action<TrayNotification>? NotificationReceived;
    public event Action? SubscriptionConnected;
    public event Action? SubscriptionDisconnected;

    public void Connect(string serverUrl, string apiKey = "")
    {
        _connectCts?.Cancel();
        _ = DisposeHubAsync();

        _serverUrl = serverUrl.TrimEnd('/');
        _apiKey = apiKey;
        IsConnected = true;
    }

    public void StartSubscription()
    {
        if (_serverUrl is null)
            return;

        _connectCts?.Cancel();
        _connectCts = new CancellationTokenSource();

        var hubUrl = string.IsNullOrEmpty(_apiKey)
            ? $"{_serverUrl}/stream"
            : $"{_serverUrl}/stream?key={Uri.EscapeDataString(_apiKey)}";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hub.On<TrayNotification>("ReceiveNotification", notif =>
            NotificationReceived?.Invoke(notif));

        _hub.On<bool>("DndChanged", enabled =>
            DndChanged?.Invoke(enabled));

        _hub.Reconnecting += ex =>
        {
            Log.Warning(ex, "SignalR reconnecting");
            SubscriptionDisconnected?.Invoke();
            return Task.CompletedTask;
        };

        _hub.Reconnected += _ =>
        {
            Log.Information("SignalR reconnected to {Url}", _serverUrl);
            SubscriptionConnected?.Invoke();
            return Task.CompletedTask;
        };

        _hub.Closed += ex =>
        {
            Log.Warning(ex, "SignalR connection closed");
            SubscriptionDisconnected?.Invoke();
            return Task.CompletedTask;
        };

        _ = Task.Run(() => ConnectLoopAsync(_connectCts.Token));
    }

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _hub!.StartAsync(ct);
                Log.Information("SignalR connected to {Url}", _serverUrl);
                SubscriptionConnected?.Invoke();
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SignalR connect failed — retrying in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    public async Task<IReadOnlyDictionary<string, byte[]>> GetIconBundleAsync(CancellationToken ct = default)
    {
        if (_serverUrl is null)
            return new Dictionary<string, byte[]>();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_serverUrl}/api/icons");
            if (!string.IsNullOrEmpty(_apiKey))
                request.Headers.Add("x-api-key", _apiKey);

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
            return null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_serverUrl}/api/version");
            if (!string.IsNullOrEmpty(_apiKey))
                request.Headers.Add("x-api-key", _apiKey);

            var json = await _http.SendAsync(request, ct)
                                  .ContinueWith(t => t.Result.Content.ReadAsStringAsync(ct), ct)
                                  .Unwrap();

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
        if (_serverUrl is null)
            return false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_serverUrl}/api/dnd");
            if (!string.IsNullOrEmpty(_apiKey))
                request.Headers.Add("x-api-key", _apiKey);

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("enabled").GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    public async Task SetDndAsync(bool enabled, CancellationToken ct = default)
    {
        if (_serverUrl is null)
            return;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"{_serverUrl}/api/dnd");
            if (!string.IsNullOrEmpty(_apiKey))
                request.Headers.Add("x-api-key", _apiKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(new { enabled }),
                System.Text.Encoding.UTF8,
                "application/json");

            await _http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SetDndAsync failed");
        }
    }

    private async Task DisposeHubAsync()
    {
        if (_hub is not null)
        {
            try { await _hub.DisposeAsync(); } catch { }
            _hub = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _connectCts = null;
        await DisposeHubAsync();
        _serverUrl = null;
        _apiKey = null;
        IsConnected = false;
    }
}
