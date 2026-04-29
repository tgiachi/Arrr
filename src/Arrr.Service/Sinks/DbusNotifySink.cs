using System.Collections.Concurrent;
using System.Diagnostics;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Service.DBus;
using Tmds.DBus;

namespace Arrr.Service.Sinks;

internal class DbusNotifySink : ISinkPlugin
{
    private readonly ConcurrentDictionary<uint, string> _pendingUrls = new();

    private Connection? _connection;
    private INotifications? _proxy;
    private ISinkContext? _context;
    private IDisposable? _actionSub;

    public string Id => "com.arrr.sink.dbus";
    public string Name => "D-Bus Notifications";
    public string Version => "1.0.0";
    public string Author => "Arrr";
    public string Description => "Delivers notifications as native desktop popups via org.freedesktop.Notifications.";
    public string Icon => "🔔";
    public PlatformType[] Platforms => [PlatformType.Linux];

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_proxy is null)
        {
            return;
        }

        try
        {
            var actions = notification.Url is not null
                ? new[] { "default", "Open" }
                : Array.Empty<string>();

            var id = await _proxy.NotifyAsync(
                notification.Source,
                0,
                notification.IconUrl ?? "",
                notification.Title,
                notification.Body,
                actions,
                new Dictionary<string, object> { ["urgency"] = notification.ToDbusUrgency() },
                -1
            );

            if (notification.Url is not null)
            {
                _pendingUrls[id] = notification.Url;
            }
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Failed to send D-Bus notification: {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;

        try
        {
            _connection = new(Address.Session);
            await _connection.ConnectAsync();
            _proxy = _connection.CreateProxy<INotifications>(
                "org.freedesktop.Notifications",
                "/org/freedesktop/Notifications"
            );

            _actionSub = await _proxy.WatchActionInvokedAsync(
                e =>
                {
                    if (e.ActionKey != "default")
                    {
                        return;
                    }

                    if (!_pendingUrls.TryRemove(e.Id, out var url))
                    {
                        return;
                    }

                    try
                    {
                        Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
                    }
                    catch (Exception ex)
                    {
                        context.Logger.LogWarning(ex, "xdg-open failed for {Url}", url);
                    }
                },
                ex => context.Logger.LogWarning(ex, "ActionInvoked signal error")
            );

            context.Logger.LogInformation("D-Bus sink connected");
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "D-Bus session bus unavailable — desktop notifications disabled");
            _connection = null;
            _proxy = null;
        }
    }

    public Task StopAsync()
    {
        _actionSub?.Dispose();
        _actionSub = null;
        _connection?.Dispose();
        _connection = null;
        _proxy = null;

        return Task.CompletedTask;
    }
}
