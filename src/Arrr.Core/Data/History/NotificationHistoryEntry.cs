using Arrr.Core.Types;

namespace Arrr.Core.Data.History;

public record NotificationHistoryEntry(
    string Id,
    string Source,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    string? IconUrl,
    NotificationPriority Priority
);
