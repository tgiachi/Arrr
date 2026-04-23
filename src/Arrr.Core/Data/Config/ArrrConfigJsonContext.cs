using System.Text.Json.Serialization;

namespace Arrr.Core.Data.Config;

[JsonSerializable(typeof(ArrrConfig))]
public partial class ArrrConfigJsonContext : JsonSerializerContext
{

}
