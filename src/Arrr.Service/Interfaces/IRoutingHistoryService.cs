using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Service.Internal;

namespace Arrr.Service.Interfaces;

internal interface IRoutingHistoryService
{
    void Record(RoutingDecision decision, Notification notification);
    IReadOnlyList<RoutingLogEntryDto> GetRecent(int limit);
}
