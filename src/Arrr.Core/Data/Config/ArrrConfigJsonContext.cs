using System.Text.Json.Serialization;

namespace Arrr.Core.Data.Config;

[JsonSerializable(typeof(ArrrConfig)), JsonSerializable(typeof(PluginEntry)), JsonSerializable(typeof(SinkEntry)),
 JsonSerializable(typeof(ArrrWebConfig)), JsonSerializable(typeof(DigestConfig)), JsonSerializable(typeof(DigestScheduleEntry)),
 JsonSerializable(typeof(RoutingConfig)), JsonSerializable(typeof(RoutingRule)), JsonSerializable(typeof(List<RoutingRule>)),
 JsonSerializable(typeof(ExtraCondition)), JsonSerializable(typeof(List<ExtraCondition>))]
public partial class ArrrConfigJsonContext : JsonSerializerContext { }
