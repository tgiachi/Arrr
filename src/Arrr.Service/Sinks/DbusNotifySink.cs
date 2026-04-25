using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.DBus;
using Tmds.DBus;

namespace Arrr.Service.Sinks;

internal class DbusNotifySink : ISinkPlugin
{
    private Connection? _connection;
    private INotifications? _proxy;
    private ISinkContext? _context;

    public string Id => "com.arrr.sink.dbus";
    public string Name => "D-Bus Notifications";
    public string Version => "1.0.0";
    public string Author => "Arrr";
    public string Description => "Delivers notifications as native desktop popups via org.freedesktop.Notifications.";
    public string Icon => "🔔";

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_proxy is null)
        {
            return;
        }

        try
        {
            await _proxy.NotifyAsync(
                notification.Source,
                0,
                notification.IconUrl ?? "",
                notification.Title,
                notification.Body,
                [],
                new Dictionary<string, object>(),
                -1
            );
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
        _connection?.Dispose();
        _connection = null;
        _proxy = null;

        return Task.CompletedTask;
    }
}
