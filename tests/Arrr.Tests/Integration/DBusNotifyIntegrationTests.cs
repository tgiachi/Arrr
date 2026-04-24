using Arrr.Service.DBus;
using Tmds.DBus;

namespace Arrr.Tests.Integration;

[TestFixture, Category("Integration"), Explicit("Requires a running D-Bus session bus and desktop environment")]
public class DBusNotifyIntegrationTests
{
    [Test]
    public async Task SendNotification_ViaSessionBus_ReturnsNonZeroId()
    {
        using var connection = new Connection(Address.Session);
        await connection.ConnectAsync();

        var proxy = connection.CreateProxy<INotifications>(
            "org.freedesktop.Notifications",
            "/org/freedesktop/Notifications"
        );

        var id = await proxy.NotifyAsync(
                     "Arrr",
                     0,
                     "",
                     "Arrr — test notifica",
                     "Hello from Arrr D-Bus integration test!",
                     [],
                     new Dictionary<string, object>(),
                     3000
                 );

        Assert.That(id, Is.GreaterThan(0u));
    }
}
