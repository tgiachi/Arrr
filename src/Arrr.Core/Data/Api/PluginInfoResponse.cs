namespace Arrr.Core.Data.Api;

public record PluginInfoResponse(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string[] Categories,
    string Icon
);
