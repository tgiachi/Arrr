using System.Text.Json.Serialization;

namespace Arrr.Core.Data.Config;

[JsonSerializable(typeof(ArrrConfig)), JsonSerializable(typeof(PluginEntry)), JsonSerializable(typeof(SinkEntry)),
 JsonSerializable(typeof(ArrrWebConfig)), JsonSerializable(typeof(DigestConfig)), JsonSerializable(typeof(DigestScheduleEntry))]
public partial class ArrrConfigJsonContext : JsonSerializerContext { }
