using Serilog;
using Tmds.DBus;

namespace Arrr.Tray.Services;

[DBusInterface("org.freedesktop.Notifications")]
public interface IFreedesktopNotifications : IDBusObject
{
    Task<uint> NotifyAsync(
        string appName,
        uint replacesId,
        string appIcon,
        string summary,
        string body,
        string[] actions,
        IDictionary<string, object> hints,
        int expireTimeout);
}

internal sealed class DbusNotificationService : IAsyncDisposable
{
    private Connection? _connection;
    private IFreedesktopNotifications? _proxy;
    private bool _available;

    public async Task InitializeAsync()
    {
        try
        {
            Log.Debug("D-Bus: connecting to session bus (Address={Addr})", Address.Session);
            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();
            Log.Debug("D-Bus: connected");

            _proxy = _connection.CreateProxy<IFreedesktopNotifications>(
                "org.freedesktop.Notifications",
                "/org/freedesktop/Notifications");
            _available = true;
            Log.Information("D-Bus notification service ready");
        }
        catch (Exception ex)
        {
            _available = false;
            Log.Warning(ex, "D-Bus notification service unavailable");
        }
    }

    public async Task ShowAsync(string title, string body)
    {
        if (!_available || _proxy is null)
        {
            Log.Warning("D-Bus ShowAsync called but service not available");
            return;
        }

        try
        {
            Log.Debug("D-Bus: sending notification title={Title}", title);
            await _proxy.NotifyAsync(
                "Arrr", 0, "dialog-information",
                title, body,
                [], new Dictionary<string, object>(), 5000);
            Log.Debug("D-Bus: notification sent");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "D-Bus notification failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        _connection = null;
        _proxy = null;
        _available = false;
    }
}
