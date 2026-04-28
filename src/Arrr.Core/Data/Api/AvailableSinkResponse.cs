namespace Arrr.Core.Data.Api;

public record AvailableSinkResponse(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string Icon,
    bool Enabled,
    bool Running,
    bool IsBuiltIn,
    bool HasConfig,
    bool HasTest
);
