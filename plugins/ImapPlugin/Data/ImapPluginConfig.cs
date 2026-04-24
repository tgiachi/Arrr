using Arrr.Core.Attributes;

namespace ImapPlugin.Data;

public class ImapPluginConfig
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";

    [Sensitive]
    public string Password { get; set; } = "";

    public string Folder { get; set; } = "INBOX";
}
