using System.Text.Json.Serialization;

namespace Arrr.Plugin.Systemd.Data;

public class JournalEntry
{
    [JsonPropertyName("MESSAGE")]
    public string Message { get; set; } = "";

    [JsonPropertyName("_SYSTEMD_UNIT")]
    public string? SystemdUnit { get; set; }

    [JsonPropertyName("SYSLOG_IDENTIFIER")]
    public string? SyslogIdentifier { get; set; }

    [JsonPropertyName("PRIORITY")]
    public string Priority { get; set; } = "6";

    [JsonPropertyName("__REALTIME_TIMESTAMP")]
    public string? RealtimeTimestamp { get; set; }
}
