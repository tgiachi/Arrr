using System.Text.Json.Serialization;

namespace Arrr.Tray.Models;

public sealed class TrayNotification
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
