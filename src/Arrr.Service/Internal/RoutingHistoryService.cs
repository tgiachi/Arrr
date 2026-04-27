using System.Collections.Concurrent;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Service.Internal.Types;

namespace Arrr.Service.Internal;

internal sealed class RoutingHistoryService
{
    private const int MaxEntries = 200;

    private readonly ConcurrentQueue<RoutingLogEntryDto> _entries = new();

    internal void Record(RoutingDecision decision, Notification notification)
    {
        if (decision.RuleName is null || decision.Action == RoutingAction.AllowAll)
        {
            return;
        }

        var action = decision.Action == RoutingAction.Block ? "blocked" : "restricted";

        _entries.Enqueue(new RoutingLogEntryDto(
            DateTimeOffset.Now,
            decision.RuleName,
            action,
            notification.Source,
            notification.Title,
            decision.SinkIds
        ));

        while (_entries.Count > MaxEntries)
        {
            _entries.TryDequeue(out _);
        }
    }

    internal IReadOnlyList<RoutingLogEntryDto> GetRecent(int limit)
        => _entries.TakeLast(Math.Min(limit, MaxEntries)).Reverse().ToList();
}
