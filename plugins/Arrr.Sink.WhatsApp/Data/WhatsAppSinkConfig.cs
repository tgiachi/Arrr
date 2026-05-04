using System.ComponentModel;

namespace Arrr.Sink.WhatsApp.Data;

public class WhatsAppSinkConfig
{
    [Description("URL of the whatsapp-bridge HTTP server (e.g. http://127.0.0.1:8765)")]
    public string BridgeUrl { get; set; } = "http://127.0.0.1:8765";

    [Description("Recipient JID — personal chat: 39123456789@s.whatsapp.net  group: GROUPID@g.us")]
    public string To { get; set; } = "";

    [Description("Message template. Placeholders: {title}, {body}, {source}")]
    public string Template { get; set; } = "*{title}*\n{body}";
}
