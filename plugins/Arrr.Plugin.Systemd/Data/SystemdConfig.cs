using System.ComponentModel;

namespace Arrr.Plugin.Systemd.Data;

public class SystemdConfig
{
    [Description("Minimum priority level to capture: emerg, alert, crit, err, warning, notice, info, debug")]
    public string MinPriority { get; set; } = "err";

    [Description("Filter to specific systemd units (e.g. ['nginx.service', 'postgresql.service']). Empty = all units.")]
    public List<string> Units { get; set; } = [];

    [Description("Truncate messages longer than this character count (0 = no limit)")]
    public int MaxMessageLength { get; set; } = 500;

    [Description("Path to the journalctl executable")]
    public string JournalctlPath { get; set; } = "/usr/bin/journalctl";
}
