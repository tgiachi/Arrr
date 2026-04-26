using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Serilog;

namespace Arrr.Core.Services;

public class EventBusService : IEventBus
{
    private readonly ILogger _logger = Log.ForContext<EventBusService>();
    private readonly Channel<IArrrEvent> _channel;
    private readonly List<(Type EventType, Func<IArrrEvent, CancellationToken, Task> Handler)> _subscribers = [];
    private readonly Lock _lock = new();
    private readonly IConfigService? _configService;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentNotifications = new();

    private CancellationTokenSource _cts = new();
    private Task _dispatchTask = Task.CompletedTask;

    public EventBusService()
    {
        _channel = Channel.CreateUnbounded<IArrrEvent>(new() { SingleReader = true });
    }

    public EventBusService(IConfigService configService)
    {
        _configService = configService;
        _channel = Channel.CreateUnbounded<IArrrEvent>(new() { SingleReader = true });
    }

    public async Task PublishAsync<T>(T evt, CancellationToken ct = default) where T : IArrrEvent
    {
        if (_configService is not null && evt is Notification notification)
        {
            var config = _configService.Config.Deduplication;

            if (config.Enabled && IsDuplicate(notification, TimeSpan.FromSeconds(config.WindowSeconds)))
            {
                _logger.Debug(
                    "[EventBus] Duplicate suppressed: {Source}/{Title}",
                    notification.Source,
                    notification.Title
                );

                return;
            }
        }

        await _channel.Writer.WriteAsync(evt, ct);
    }

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _dispatchTask = Task.Run(() => DispatchLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync();

        try
        {
            await _dispatchTask.WaitAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IArrrEvent
    {
        lock (_lock)
        {
            _subscribers.Add((typeof(T), (evt, ct) => handler((T)evt, ct)));
        }
    }

    private static string ComputeKey(Notification n)
    {
        var raw = Encoding.UTF8.GetBytes($"{n.Source}|{n.Title}|{n.Body}");

        return Convert.ToHexString(SHA256.HashData(raw));
    }

    private async Task DispatchLoopAsync(CancellationToken ct)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            List<Func<IArrrEvent, CancellationToken, Task>> handlers;

            lock (_lock)
            {
                handlers = _subscribers
                           .Where(s => s.EventType.IsAssignableFrom(evt.GetType()))
                           .Select(s => s.Handler)
                           .ToList();
            }

            await Task.WhenAll(
                handlers.Select(
                    async handler =>
                    {
                        try
                        {
                            await handler(evt, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "[EventBus] Handler error");
                        }
                    }
                )
            );
        }
    }

    private bool IsDuplicate(Notification n, TimeSpan window)
    {
        var key = ComputeKey(n);
        var now = DateTimeOffset.UtcNow;

        foreach (var (k, seen) in _recentNotifications)
        {
            if (now - seen > window)
            {
                _recentNotifications.TryRemove(k, out _);
            }
        }

        if (_recentNotifications.TryGetValue(key, out var lastSeen) && now - lastSeen <= window)
        {
            return true;
        }

        _recentNotifications[key] = now;

        return false;
    }
}
