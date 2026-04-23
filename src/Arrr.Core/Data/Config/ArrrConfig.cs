namespace Arrr.Core.Data.Config;

public class ArrrConfig
{
    public string SocketPath { get; set; } = "/tmp/arrr.sock";
    public ArrrWebConfig Web { get; set; } = new();
    public List<PluginEntry> Plugins { get; set; } = [];
}
