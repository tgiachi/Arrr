namespace Arrr.Core.Data.Api;

public record AvailablePluginResponse(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string[] Categories,
    string Icon,
    bool Enabled,
    bool Running
);
