using Tmds.DBus;

namespace Arrr.Service.DBus;

[DBusInterface("org.freedesktop.Notifications")]
public interface INotifications : IDBusObject
{
    Task<uint> NotifyAsync(
        string appName,
        uint replacesId,
        string appIcon,
        string summary,
        string body,
        string[] actions,
        IDictionary<string, object> hints,
        int expireTimeout
    );

    Task<IDisposable> WatchActionInvokedAsync(
        Action<(uint Id, string ActionKey)> handler,
        Action<Exception>? onError = null
    );
}
