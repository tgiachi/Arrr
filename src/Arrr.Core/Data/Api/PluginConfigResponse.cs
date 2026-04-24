using System.Text.Json;

namespace Arrr.Core.Data.Api;

public record PluginConfigResponse(JsonElement Values, ConfigFieldInfo[] Schema);
