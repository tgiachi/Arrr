using System.Text.Json.Serialization;

namespace Arrr.Core.Data.Config;

[JsonSerializable(typeof(ArrrConfig)), JsonSerializable(typeof(PluginEntry)), JsonSerializable(typeof(ArrrWebConfig))]
public partial class ArrrConfigJsonContext : JsonSerializerContext { }
