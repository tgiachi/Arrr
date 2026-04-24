using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace MyPlugin;

public class MyPlugin : IPollingPlugin
{
    public string Id          => "com.example.myplugin";
    public string Name        => "MyPlugin";
    public string Version     => "1.0.0";
    public string Author      => "Your Name";
    public string Description => "Short description of what MyPlugin does.";
    public string[] Categories => [];
    public string Icon        => "";

    public TimeSpan Interval  => TimeSpan.Parse("00:05:00");

    // StartAsync is managed by the Arrr host — leave it empty.
    public Task StartAsync(IPluginContext context, CancellationToken ct) => Task.CompletedTask;

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        // TODO: fetch data from your source and publish notifications.
        await context.EventBus.PublishAsync(
            new Notification(
                Id:        Guid.NewGuid(),
                Source:    Id,
                Title:     "Hello from MyPlugin",
                Body:      "Replace this with real data.",
                Timestamp: DateTimeOffset.UtcNow,
                IconUrl:   null
            ),
            ct
        );
    }
}
