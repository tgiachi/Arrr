using System.ComponentModel;
using Arrr.Core.Attributes;

namespace MySink;

public class MySinkConfig
{
    // Add your configuration properties here.
    // Mark sensitive fields (passwords, tokens) with [Sensitive]
    // so they are automatically encrypted at rest.
    [Description("Example setting")]
    public string ExampleSetting { get; set; } = "";
}
