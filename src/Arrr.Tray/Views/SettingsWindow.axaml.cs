using Arrr.Tray.ViewModels;
using Avalonia.Controls;

namespace Arrr.Tray.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is SettingsViewModel vm)
        {
            vm.CloseRequested += Close;
        }
    }
}
