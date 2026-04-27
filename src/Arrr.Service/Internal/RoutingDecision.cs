using Arrr.Service.Internal.Types;

namespace Arrr.Service.Internal;

internal record RoutingDecision(
    IReadOnlyList<string> SinkIds,
    string? RuleName,
    RoutingAction Action
);
