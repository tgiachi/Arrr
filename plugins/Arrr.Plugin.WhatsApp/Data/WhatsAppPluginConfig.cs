using System.ComponentModel;

namespace WhatsAppPlugin.Data;

public class WhatsAppPluginConfig
{
    [Description("Path to the whatsapp-bridge binary. Relative paths are resolved next to the plugin DLL.")]
    public string BridgePath { get; set; } = "whatsapp-bridge";

    [Description("Sender display names or group JID user parts to monitor. Empty = all incoming messages.")]
    public List<string> MonitoredChats { get; set; } = [];
}
