using CommunityToolkit.Mvvm.ComponentModel;

namespace Arrr.Tray.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string TrayVersion { get; }
    public string ServiceVersion { get; }
    public string Author { get; } = "Tom <tom@orivega.io>";
    public string RepoUrl { get; } = "https://github.com/tgiachi/Arrr";

    public AboutViewModel(string trayVersion, string serviceVersion)
    {
        TrayVersion = trayVersion;
        ServiceVersion = serviceVersion;
    }
}
