using Arrr.Tray.Services;
using Arrr.Tray.Types;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Arrr.Tray.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly ArrrServiceClient _client;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DndLabel))]
    private bool _dndEnabled;

    public string DndLabel => DndEnabled ? "Disable DND" : "Enable DND";

    public event Action<bool>? DndStateChanged;
    public event Action<string>? ServerVersionChanged;
    public event Action<bool>? ConnectionStateChanged;
    public event Action<NotificationProviderType>? NotificationProviderChanged;

    public TrayViewModel(ArrrServiceClient client, SettingsService settingsService)
    {
        _client = client;
        _settingsService = settingsService;

        client.DndChanged += enabled =>
        {
            DndEnabled = enabled;
            DndStateChanged?.Invoke(enabled);
        };

        client.SubscriptionConnected    += () => ConnectionStateChanged?.Invoke(true);
        client.SubscriptionDisconnected += () => ConnectionStateChanged?.Invoke(false);
    }

    public async Task InitializeAsync()
    {
        Log.Debug("InitializeAsync — fetching DND state");
        try
        {
            DndEnabled = await _client.GetDndAsync();
            DndStateChanged?.Invoke(DndEnabled);
            Log.Debug("DND state = {DndEnabled}", DndEnabled);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GetDndAsync failed — will retry via subscription");
        }

        Log.Debug("InitializeAsync — fetching version");
        var version = await _client.GetVersionAsync();
        if (version is not null)
        {
            _serviceVersion = version;
            Log.Information("Service version: {Version}", version);
            ServerVersionChanged?.Invoke($"Arrr v{version}");
            ConnectionStateChanged?.Invoke(true);
        }
        else
        {
            Log.Warning("GetVersionAsync returned null — service unreachable or wrong URL");
            ConnectionStateChanged?.Invoke(false);
        }

        Log.Debug("Starting SignalR subscription");
        _client.StartSubscription();
    }

    [RelayCommand]
    private async Task ToggleDndAsync()
    {
        try
        {
            var newState = !DndEnabled;
            await _client.SetDndAsync(newState);
            DndEnabled = newState;
            DndStateChanged?.Invoke(newState);
            Log.Debug("DND toggled → {State}", newState);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ToggleDnd failed");
        }
    }

    private string _serviceVersion = "–";
    private Views.SettingsWindow? _settingsWindow;
    private Views.AboutWindow? _aboutWindow;

    [RelayCommand]
    private void OpenSettings()
    {
        Log.Debug("OpenSettings invoked — existing window = {HasWindow}", _settingsWindow is not null);

        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Log.Debug("Creating SettingsWindow (inside UIThread.Post)");
            try
            {
                var settings = _settingsService.Load();
                var vm = new SettingsViewModel(settings, _settingsService, _client);
                vm.NotificationProviderChanged += p => NotificationProviderChanged?.Invoke(p);
                var window = new Views.SettingsWindow { DataContext = vm };

                window.Closed += (_, _) =>
                {
                    Log.Debug("SettingsWindow closed");
                    _settingsWindow = null;
                };
                _settingsWindow = window;

                Log.Debug("Calling window.Show()");
                window.Show();
                Log.Debug("Calling window.Activate()");
                window.Activate();
                Log.Debug("SettingsWindow shown");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open SettingsWindow");
                _settingsWindow = null;
            }
        });
    }

    [RelayCommand]
    private void OpenAbout()
    {
        Log.Debug("OpenAbout invoked");

        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var trayVer = typeof(TrayViewModel).Assembly.GetName().Version?.ToString(3) ?? "dev";
                var vm = new AboutViewModel(trayVer, _serviceVersion);
                var window = new Views.AboutWindow { DataContext = vm };

                window.Closed += (_, _) =>
                {
                    Log.Debug("AboutWindow closed");
                    _aboutWindow = null;
                };
                _aboutWindow = window;

                window.Show();
                window.Activate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open AboutWindow");
                _aboutWindow = null;
            }
        });
    }

    [RelayCommand]
    private void Exit()
    {
        Log.Information("Exit requested");
        try
        {
            _ = _client.DisposeAsync().AsTask();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Client dispose error during exit");
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
        {
            Log.Debug("Calling lifetime.Shutdown()");
            lifetime.Shutdown();
        }
        else
        {
            Log.Warning("ApplicationLifetime not available — calling Environment.Exit(0)");
            Environment.Exit(0);
        }
    }
}
