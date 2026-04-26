using System.ComponentModel;
using Arrr.Core.Attributes;

namespace TelegramPlugin.Data;

public class TelegramPluginConfig
{
    [Description("Numeric app ID from my.telegram.org → API development tools")]
    public int ApiId { get; set; }

    [Sensitive, Description("App hash from my.telegram.org (stored encrypted)")]
    public string ApiHash { get; set; } = "";

    [Description("Your phone number with country code, e.g. +393331234567")]
    public string PhoneNumber { get; set; } = "";

    [Sensitive, Description("Telegram 2FA password, if enabled on your account (stored encrypted). Leave empty if not set.")]
    public string TwoFactorPassword { get; set; } = "";

    [Description("Chat display names or usernames to monitor. Empty = all incoming messages.")]
    public List<string> MonitoredChats { get; set; } = [];
}
