using System.Text.Json.Serialization;

namespace Arrr.Core.Data.Config;

[JsonSerializable(typeof(ArrrConfig))]
[JsonSerializable(typeof(PluginEntry))]
public partial class ArrrConfigJsonContext : JsonSerializerContext
{
}
