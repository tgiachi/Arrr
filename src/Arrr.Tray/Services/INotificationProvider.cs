namespace Arrr.Tray.Services;

internal interface INotificationProvider
{
    Task InitializeAsync();

    Task ShowAsync(string title, string body, string? iconUrl = null, string? source = null, string? url = null);

    ValueTask DisposeAsync();
}
