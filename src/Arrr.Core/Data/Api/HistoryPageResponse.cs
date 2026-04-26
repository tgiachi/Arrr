using Arrr.Core.Data.History;

namespace Arrr.Core.Data.Api;

public record HistoryPageResponse(
    IReadOnlyList<NotificationHistoryEntry> Items,
    int Total,
    int Page,
    int Limit
);
