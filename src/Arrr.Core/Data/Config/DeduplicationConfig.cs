using System.ComponentModel;

namespace Arrr.Core.Data.Config;

public class DeduplicationConfig
{
    [Description("Enable duplicate notification suppression")]
    public bool Enabled { get; set; } = false;

    [Description("Time window in seconds during which identical notifications (same source/title/body) are suppressed")]
    public int WindowSeconds { get; set; } = 300;
}
