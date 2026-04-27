namespace Arrr.Core.Data.Api;

public record RoutingLogEntryDto(
    DateTimeOffset Timestamp,
    string RuleName,
    string Action,
    string NotificationSource,
    string NotificationTitle,
    IReadOnlyList<string> TargetSinks
);
