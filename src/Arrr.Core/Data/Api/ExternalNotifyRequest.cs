namespace Arrr.Core.Data.Api;

public record ExternalNotifyRequest(
    string Source,
    string Title,
    string Body,
    string? IconUrl
);
