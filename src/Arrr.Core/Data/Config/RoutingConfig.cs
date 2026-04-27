using System.ComponentModel;

namespace Arrr.Core.Data.Config;

public class RoutingConfig
{
    [Description("Enable the routing rules engine. When false, all notifications go to all running sinks.")]
    public bool Enabled { get; set; } = false;

    [Description("Ordered list of routing rules. First matching rule wins.")]
    public List<RoutingRule> Rules { get; set; } =
    [
        new RoutingRule
        {
            Name = "default — allow all",
            Enabled = true,
            SourcePattern = "*",
            Block = false,
            AllowSinks = []
        }
    ];
}
