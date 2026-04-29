using System.Text.Json.Serialization;
using Arrr.Tray.Types;

namespace Arrr.Tray.Models;

public class AppSettings
{
    [JsonPropertyName("serverUrl")]
    public string ServerUrl { get; set; } = "http://localhost:5150";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("notificationProvider")]
    public NotificationProviderType NotificationProvider { get; set; } = NotificationProviderType.DBus;
}
