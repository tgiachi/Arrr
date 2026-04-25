using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MySink;

public class MySink : ISinkPlugin, IConfigurablePlugin
{
    private MySinkConfig _config = new();
    private ISinkContext? _context;

    public string Id          => "com.example.mysink";
    public string Name        => "MySink";
    public string Version     => "1.0.0";
    public string Author      => "Your Name";
    public string Description => "Short description of what MySink does.";
    public string Icon        => "📤";
    public Type   ConfigType  => typeof(MySinkConfig);

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<MySinkConfig>(ct);
        context.Logger.LogInformation("MySink started.");
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        // TODO: deliver the notification to your output channel.
        _context?.Logger.LogInformation("[{Source}] {Title}", notification.Source, notification.Title);
        await Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _context = null;
        return Task.CompletedTask;
    }
}
