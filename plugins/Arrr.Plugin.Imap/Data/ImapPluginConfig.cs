using System.ComponentModel;

namespace ImapPlugin.Data;

public class ImapPluginConfig
{
    [Description("How often to poll all accounts for new messages, in minutes")]
    public int PollIntervalMinutes { get; set; } = 2;

    [Description("One entry per email account to monitor")]
    public List<ImapAccountConfig> Accounts { get; set; } = [];
}
