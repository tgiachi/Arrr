using Tmds.DBus;

namespace Arrr.Tray.Services;

[DBusInterface("org.freedesktop.Notifications")]
internal interface IFreedesktopNotifications : IDBusObject
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
            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();
            _proxy = _connection.CreateProxy<IFreedesktopNotifications>(
                "org.freedesktop.Notifications",
                "/org/freedesktop/Notifications");
            _available = true;
        }
        catch
        {
            _available = false;
        }
    }

    public async Task ShowAsync(string title, string body)
    {
        if (!_available || _proxy is null)
        {
            return;
        }

        try
        {
            await _proxy.NotifyAsync(
                "Arrr", 0, "dialog-information",
                title, body,
                [], new Dictionary<string, object>(), 5000);
        }
        catch
        {
            // D-Bus unavailable at runtime — silently ignore
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
