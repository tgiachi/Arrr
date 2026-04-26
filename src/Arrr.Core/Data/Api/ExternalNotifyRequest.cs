using Arrr.Core.Types;

namespace Arrr.Core.Data.Api;

public record ExternalNotifyRequest(
    string Source,
    string Title,
    string Body,
    string? IconUrl,
    NotificationPriority Priority = NotificationPriority.Normal,
    string? Url = null,
    Dictionary<string, string>? Extras = null
);
