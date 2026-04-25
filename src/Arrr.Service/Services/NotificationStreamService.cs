using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Arrr.Service.Services;

internal class NotificationStreamService : IHostedService
{
    private readonly IHubContext<NotificationStreamHub> _hub;
    private readonly IEventBus _bus;

    public NotificationStreamService(IHubContext<NotificationStreamHub> hub, IEventBus bus)
    {
        _hub = hub;
        _bus = bus;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _bus.Subscribe<Notification>(
            async (notification, token) =>
                await _hub.Clients.All.SendAsync("ReceiveNotification", notification, token)
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
