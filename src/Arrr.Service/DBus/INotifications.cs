using Tmds.DBus;

namespace Arrr.Service.DBus;

[DBusInterface("org.freedesktop.Notifications")]
internal interface INotifications : IDBusObject
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
