using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalRSink.Data;

namespace SignalRSink;

public class SignalRSinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private WebApplication? _app;
    private IHubContext<NotificationHub>? _hubContext;
    private ISinkContext? _context;

    public string Id          => "com.arrr.sink.signalr";
    public string Name        => "SignalR";
    public string Version     => "1.0.0";
    public string Author      => "Arrr";
    public string Description => "Broadcasts notifications to SignalR clients via a hub.";
    public string Icon        => "📡";
    public Type   ConfigType  => typeof(SignalRSinkConfig);

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        if (_app is not null)
            await StopAsync();

        _context = context;
        var config = await context.LoadConfigAsync<SignalRSinkConfig>(ct);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = "SignalRSink" });
        builder.WebHost.UseSetting("urls", $"http://0.0.0.0:{config.Port}");
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();

        _app = builder.Build();

        var hubPath = "/" + config.HubPath.TrimStart('/');
        _app.MapHub<NotificationHub>(hubPath);

        await _app.StartAsync(ct);
        _hubContext = _app.Services.GetRequiredService<IHubContext<NotificationHub>>();

        context.Logger.LogInformation("SignalR sink hub at http://0.0.0.0:{Port}/{HubPath}", config.Port, config.HubPath.TrimStart('/'));
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_hubContext is null)
            return;

        await _hubContext.Clients.All.SendAsync("ReceiveNotification", notification, ct);
    }

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
