using Arrr.Tray.Models;
using Arrr.Tray.Services;
using Arrr.Tray.Types;
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

    [ObservableProperty]
    private NotificationProviderType _notificationProvider;

    public IReadOnlyList<NotificationProviderType> NotificationProviders { get; } =
        Enum.GetValues<NotificationProviderType>();

    public event Action? CloseRequested;
    public event Action<NotificationProviderType>? NotificationProviderChanged;

    public SettingsViewModel(AppSettings settings, SettingsService settingsService, ArrrServiceClient client)
    {
        _settingsService = settingsService;
        _client = client;
        _serverUrl = settings.ServerUrl;
        _apiKey = settings.ApiKey;
        _notificationProvider = settings.NotificationProvider;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings
        {
            ServerUrl = ServerUrl,
            ApiKey = ApiKey,
            NotificationProvider = NotificationProvider
        };
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

        NotificationProviderChanged?.Invoke(NotificationProvider);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke();
}
