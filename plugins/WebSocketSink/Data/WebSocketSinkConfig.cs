using System.ComponentModel;

namespace WebSocketSink.Data;

public class WebSocketSinkConfig
{
    [Description("HTTP port Kestrel listens on for WebSocket upgrade requests")]
    public int Port { get; set; } = 5002;

    [Description("URL path — clients connect to ws://host:{Port}/{Path}")]
    public string Path { get; set; } = "ws";
}
