using System.ComponentModel;
using Arrr.Core.Attributes;

namespace ImapPlugin.Data;

public class ImapAccountConfig
{
    [Description("Display name for this account (used in notification title)")]
    public string Name { get; set; } = "";

    [Description("IMAP server hostname, e.g. imap.gmail.com")]
    public string Host { get; set; } = "";

    [Description("IMAP port — 993 for SSL (recommended), 143 for STARTTLS")]
    public int Port { get; set; } = 993;

    [Description("Use SSL/TLS connection (recommended)")]
    public bool UseSsl { get; set; } = true;

    [Description("Email address / login username")]
    public string Username { get; set; } = "";

    [Sensitive, Description("Account password or app-specific password (stored encrypted)")]
    public string Password { get; set; } = "";

    [Description("Mailbox folder to monitor, e.g. INBOX")]
    public string Folder { get; set; } = "INBOX";
}
