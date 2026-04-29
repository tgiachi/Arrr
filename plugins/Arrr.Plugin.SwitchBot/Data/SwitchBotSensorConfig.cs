using System.ComponentModel;

namespace Arrr.Plugin.SwitchBot.Data;

public class SwitchBotSensorConfig
{
    [Description("Device ID from the SwitchBot app or API (e.g. AABBCCDDEEFF)")]
    public string DeviceId { get; set; } = "";

    [Description("Display name for this sensor (shown in notifications and digest)")]
    public string Name { get; set; } = "";

    [Description("Alert when temperature exceeds this value in °C (null = no alert)")]
    public double? TempMaxC { get; set; }

    [Description("Alert when temperature drops below this value in °C (null = no alert)")]
    public double? TempMinC { get; set; }

    [Description("Alert when humidity exceeds this percentage (null = no alert)")]
    public double? HumidityMax { get; set; }

    [Description("Alert when humidity drops below this percentage (null = no alert)")]
    public double? HumidityMin { get; set; }
}
