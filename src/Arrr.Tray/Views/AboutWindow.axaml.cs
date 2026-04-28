using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Arrr.Tray.ViewModels;

namespace Arrr.Tray.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
        => Close();

    private void OnRepoClick(object? sender, RoutedEventArgs e)
    {
        var url = (DataContext as AboutViewModel)?.RepoUrl;
        if (url is null) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}
