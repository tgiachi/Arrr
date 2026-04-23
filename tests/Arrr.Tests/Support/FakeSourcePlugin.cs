using System.Threading.Channels;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeSourcePlugin : ISourcePlugin
{
    private readonly IReadOnlyList<Notification> _notifications;
    private readonly Exception? _throws;

    public string Name { get; }
    public string Icon => "fake";

    public FakeSourcePlugin(string name, IReadOnlyList<Notification>? notifications = null, Exception? throws = null)
    {
        Name = name;
        _notifications = notifications ?? [];
        _throws = throws;
    }

    public async Task StartAsync(ChannelWriter<Notification> writer, CancellationToken ct)
    {
        if (_throws is not null)
            throw _throws;

        foreach (var n in _notifications)
        {
            await writer.WriteAsync(n, ct);
        }
    }
}
