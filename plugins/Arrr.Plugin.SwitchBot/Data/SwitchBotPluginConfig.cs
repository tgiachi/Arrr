using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Plugin.SwitchBot.Data;

public class SwitchBotPluginConfig
{
    [Description("SwitchBot API token (from the app: Profile → Preferences → App Version ×10 tap)")]
    [Sensitive]
    public string Token { get; set; } = "";

    [Description("SwitchBot API secret (same location as token)")]
    [Sensitive]
    public string Secret { get; set; } = "";

    [Description("How often to poll sensors, in minutes")]
    public int PollIntervalMinutes { get; set; } = 10;

    [Description("List of sensors to monitor")]
    public List<SwitchBotSensorConfig> Sensors { get; set; } = [];

    [Description("Section heading used in digest notifications")]
    public string DigestSectionTitle { get; set; } = "Sensors";
}
