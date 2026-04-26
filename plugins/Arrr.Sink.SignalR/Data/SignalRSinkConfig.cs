using System.ComponentModel;

namespace SignalRSink.Data;

public class SignalRSinkConfig
{
    [Description("HTTP port Kestrel listens on for SignalR connections")]
    public int Port { get; set; } = 5003;

    [Description("Hub URL path — clients connect to http://host:{Port}/{HubPath}")]
    public string HubPath { get; set; } = "notifications";
}
