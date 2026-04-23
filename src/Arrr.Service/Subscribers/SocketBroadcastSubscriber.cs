using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.Internal;

namespace Arrr.Service.Subscribers;

internal class SocketBroadcastSubscriber
{
    public SocketBroadcastSubscriber(IEventBus eventBus, UnixSocketServer socketServer)
    {
        eventBus.Subscribe<Notification>(socketServer.BroadcastAsync);
    }
}
