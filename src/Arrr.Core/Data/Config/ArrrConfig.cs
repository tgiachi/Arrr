namespace Arrr.Core.Data.Config;

public class ArrrConfig
{
    public string SocketPath { get; set; } = "/tmp/arrr.sock";
    public string ApiKey { get; set; } = "";
    public bool IsDebug { get; set; } = false;
    public ArrrWebConfig Web { get; set; } = new();
    public List<PluginEntry> Plugins { get; set; } = [];
    public List<SinkEntry>   Sinks   { get; set; } = [];
}
