using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.DBus;
using Serilog;
using Tmds.DBus;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Subscribers;

internal class DBusNotifySubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly ILogger _logger = Log.ForContext<DBusNotifySubscriber>();
    private Connection? _connection;

    public DBusNotifySubscriber(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _connection = new Connection(Address.Session);
            await _connection.ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "D-Bus session bus unavailable — desktop notifications disabled");
            _connection = null;
            return;
        }

        var proxy = _connection.CreateProxy<INotifications>(
            "org.freedesktop.Notifications",
            "/org/freedesktop/Notifications"
        );

        _eventBus.Subscribe<Notification>(async (notification, ct) =>
        {
            try
            {
                await proxy.NotifyAsync(
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
                _logger.Error(ex, "Failed to send D-Bus notification: {Title}", notification.Title);
            }
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _connection?.Dispose();
        _connection = null;
        return Task.CompletedTask;
    }
}
