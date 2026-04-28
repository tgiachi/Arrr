using System.Windows.Input;
using Arrr.Tray.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arrr.Tray.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly ArrrGrpcClient _grpc;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DndLabel))]
    private bool _dndEnabled;

    public string DndLabel => DndEnabled ? "Disable DND" : "Enable DND";

    public event Action<bool>? DndStateChanged;
    public event Action<string>? ServerVersionChanged;

    public TrayViewModel(ArrrGrpcClient grpc, SettingsService settingsService)
    {
        _grpc = grpc;
        _settingsService = settingsService;

        grpc.DndChanged += enabled =>
        {
            DndEnabled = enabled;
            DndStateChanged?.Invoke(enabled);
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            DndEnabled = await _grpc.GetDndAsync();
            DndStateChanged?.Invoke(DndEnabled);
        }
        catch
        {
            // Service not yet reachable — will update via subscription
        }

        var version = await _grpc.GetVersionAsync();
        if (version is not null)
        {
            ServerVersionChanged?.Invoke($"Arrr v{version}");
        }

        _grpc.StartSubscription();
    }

    [RelayCommand]
    private async Task ToggleDndAsync()
    {
        try
        {
            var newState = !DndEnabled;
            await _grpc.SetDndAsync(newState);
            DndEnabled = newState;
            DndStateChanged?.Invoke(newState);
        }
        catch
        {
            // Ignore if service unreachable
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settings = _settingsService.Load();
        var vm = new SettingsViewModel(settings, _settingsService, _grpc);
        var window = new Views.SettingsWindow { DataContext = vm };
        window.Show();
    }

    [RelayCommand]
    private void Exit()
    {
        _grpc.Dispose();

        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
