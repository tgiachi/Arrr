namespace Arrr.Core.Data.Notifications;

public record Notification(
    Guid Id,
    string Source,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    string? IconUrl
);
