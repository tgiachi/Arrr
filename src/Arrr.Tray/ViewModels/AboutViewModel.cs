using CommunityToolkit.Mvvm.ComponentModel;

namespace Arrr.Tray.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string TrayVersion { get; }
    public string ServiceVersion { get; }

    public AboutViewModel(string trayVersion, string serviceVersion)
    {
        TrayVersion = trayVersion;
        ServiceVersion = serviceVersion;
    }
}
