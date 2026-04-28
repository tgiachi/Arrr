using Arrr.Tray.Models;
using Arrr.Tray.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arrr.Tray.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ArrrServiceClient _client;

    [ObservableProperty]
    private string _serverUrl;

    [ObservableProperty]
    private string _apiKey;

    public event Action? CloseRequested;

    public SettingsViewModel(AppSettings settings, SettingsService settingsService, ArrrServiceClient client)
    {
        _settingsService = settingsService;
        _client = client;
        _serverUrl = settings.ServerUrl;
        _apiKey = settings.ApiKey;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings { ServerUrl = ServerUrl, ApiKey = ApiKey };
        _settingsService.Save(settings);

        try
        {
            _client.Connect(ServerUrl, ApiKey);
            _client.StartSubscription();
        }
        catch
        {
            // Will retry via reconnect loop
        }

        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke();
}
