using Arrr.Core.Attributes;

namespace TelegramPlugin.Data;

public class TelegramPluginConfig
{
    public int ApiId { get; set; }

    [Sensitive]
    public string ApiHash { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    [Sensitive]
    public string TwoFactorPassword { get; set; } = "";

    /// <summary>
    /// Chat usernames or display names to monitor.
    /// Empty list = monitor all incoming private messages and group mentions.
    /// </summary>
    public List<string> MonitoredChats { get; set; } = [];
}
