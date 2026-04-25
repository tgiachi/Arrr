using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeSourcePlugin : ISourcePlugin
{
    private readonly IReadOnlyList<Notification> _notifications;
    private readonly Exception? _throws;

    public string Id { get; }
    public string Name { get; }
    public string Version => "1.0.0";
    public string Author => "test";
    public string Description => "fake plugin for tests";
    public string[] Categories => [];
    public string Icon => "fake";

    public FakeSourcePlugin(string id, IReadOnlyList<Notification>? notifications = null, Exception? throws = null)
    {
        Id = id;
        Name = id.Split('.').Last();
        _notifications = notifications ?? [];
        _throws = throws;
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        if (_throws is not null)
        {
            throw _throws;
        }

        foreach (var n in _notifications)
        {
            await context.EventBus.PublishAsync(n, ct);
        }
    }
}
