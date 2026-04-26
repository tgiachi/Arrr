namespace Arrr.Sink.WindowsNotify.Data;

public class WindowsNotifyConfig
{
    [Description("Application name shown in the Windows notification header")]
    public string AppName { get; set; } = "Arrr";

    [Description("Show the notification source as attribution text beneath the body")]
    public bool ShowSource { get; set; } = true;
}
