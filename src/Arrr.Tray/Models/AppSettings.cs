using System.Text.Json.Serialization;

namespace Arrr.Tray.Models;

public class AppSettings
{
    [JsonPropertyName("serverUrl")]
    public string ServerUrl { get; set; } = "http://localhost:5150";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("grpcUrl")]
    public string GrpcUrl { get; set; } = "http://localhost:5151";
}
