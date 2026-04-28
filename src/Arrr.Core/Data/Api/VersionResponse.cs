namespace Arrr.Core.Data.Api;

public record VersionResponse(string Version, string RuntimeVersion, string Os, bool IsDebug);
