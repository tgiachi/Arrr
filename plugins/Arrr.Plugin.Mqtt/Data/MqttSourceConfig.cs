using System.ComponentModel;
using Arrr.Core.Attributes;

namespace MqttSource.Data;

public class MqttSourceConfig
{
    [Description("MQTT broker hostname or IP")]
    public string BrokerHost { get; set; } = "localhost";

    [Description("MQTT broker port (default 1883, TLS default 8883)")]
    public int BrokerPort { get; set; } = 1883;

    [Description("MQTT topic filter to subscribe to (supports wildcards: # and +)")]
    public string Topic { get; set; } = "#";

    [Description("Client ID sent to the broker — leave empty to auto-generate")]
    public string ClientId { get; set; } = "";

    [Description("Broker username (optional)")]
    public string Username { get; set; } = "";

    [Description("Broker password (optional)"), Sensitive]
    public string Password { get; set; } = "";

    [Description("Enable TLS/SSL connection to the broker")]
    public bool UseTls { get; set; } = false;

    [Description("Notification title template — {topic} and {payload} replaced at runtime")]
    public string TitleTemplate { get; set; } = "MQTT: {topic}";
}
