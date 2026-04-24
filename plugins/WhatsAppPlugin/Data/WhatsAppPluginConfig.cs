using System.ComponentModel;

namespace WhatsAppPlugin.Data;

public class WhatsAppPluginConfig
{
    [Description("Absolute path to the compiled whatsapp-bridge binary. Build with: cd plugins/WhatsAppPlugin/bridge && ./build.sh")]
    public string BridgePath { get; set; } = "";

    [Description("Sender display names or group JID user parts to monitor. Empty = all incoming messages.")]
    public List<string> MonitoredChats { get; set; } = [];
}
