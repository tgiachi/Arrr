namespace Arrr.Core.Data.Config;

public class ArrrConfig
{
    public string SocketPath { get; set; } = "/tmp/arrr.sock";
    public int HttpPort { get; set; } = 5150;
    public List<PluginEntry> Plugins { get; set; } = [];
}
