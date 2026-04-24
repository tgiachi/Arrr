# D-Bus Notify Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Forward every `Notification` event from `IEventBus` to the system D-Bus `org.freedesktop.Notifications` daemon as a native desktop popup.

**Architecture:** A new `DBusNotifySubscriber` (`IHostedService`) opens a D-Bus session-bus connection in `StartAsync`, obtains a proxy for `INotifications`, and registers an `IEventBus` subscription. If the session bus is unavailable (headless server), it logs a Warning and exits cleanly — the rest of the service continues unaffected. The `INotifications` proxy interface lives in `Arrr.Service/DBus/`.

**Tech Stack:** .NET 10, `Tmds.DBus 0.21.2`, Serilog `Log.ForContext<T>()`, NUnit 4.x.

---

### Task 1: Add Tmds.DBus package and create INotifications interface

**Files:**
- Modify: `src/Arrr.Service/Arrr.Service.csproj`
- Create: `src/Arrr.Service/DBus/INotifications.cs`

- [ ] **Step 1: Add Tmds.DBus package reference**

Edit `src/Arrr.Service/Arrr.Service.csproj` — add inside the existing `<ItemGroup>` with packages:

```xml
<PackageReference Include="Tmds.DBus" Version="0.21.2"/>
```

Full updated ItemGroup:

```xml
<ItemGroup>
    <PackageReference Include="ConsoleAppFramework" Version="5.7.13">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Serilog.Extensions.Logging" Version="10.0.0"/>
    <PackageReference Include="Serilog.Sinks.Console" Version="6.1.1"/>
    <PackageReference Include="Serilog.Sinks.File" Version="7.0.0"/>
    <PackageReference Include="Tmds.DBus" Version="0.21.2"/>
</ItemGroup>
```

- [ ] **Step 2: Restore packages**

Run: `dotnet restore src/Arrr.Service/Arrr.Service.csproj`

Expected: no errors, `Tmds.DBus` appears in restore output.

- [ ] **Step 3: Create DBus directory and INotifications interface**

Create `src/Arrr.Service/DBus/INotifications.cs`:

```csharp
using Tmds.DBus;

namespace Arrr.Service.DBus;

[DBusInterface("org.freedesktop.Notifications")]
internal interface INotifications : IDBusObject
{
    Task<uint> NotifyAsync(
        string appName,
        uint replacesId,
        string appIcon,
        string summary,
        string body,
        string[] actions,
        IDictionary<string, object> hints,
        int expireTimeout);
}
```

- [ ] **Step 4: Build to verify interface compiles**

Run: `dotnet build src/Arrr.Service/Arrr.Service.csproj`

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Arrr.Service/Arrr.Service.csproj src/Arrr.Service/DBus/INotifications.cs
git commit -m "feat(dbus): add Tmds.DBus package and INotifications interface"
```

---

### Task 2: Implement DBusNotifySubscriber

**Files:**
- Create: `src/Arrr.Service/Subscribers/DBusNotifySubscriber.cs`
- Create: `tests/Arrr.Tests/Service/DBusNotifySubscriberTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Arrr.Tests/Service/DBusNotifySubscriberTests.cs`:

```csharp
using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;
using Arrr.Service.Subscribers;

namespace Arrr.Tests.Service;

[TestFixture]
public class DBusNotifySubscriberTests
{
    [Test]
    public async Task StartAsync_WhenSessionBusUnavailable_DoesNotThrow()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        await bus.StartAsync(cts.Token);

        var subscriber = new DBusNotifySubscriber(bus);

        // On a CI/headless environment the session bus is absent — must not throw
        Assert.DoesNotThrowAsync(() => subscriber.StartAsync(cts.Token));

        await subscriber.StopAsync(cts.Token);
        await bus.StopAsync(cts.Token);
    }

    [Test]
    public async Task StopAsync_WhenNeverConnected_DoesNotThrow()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        var subscriber = new DBusNotifySubscriber(bus);

        Assert.DoesNotThrowAsync(() => subscriber.StopAsync(cts.Token));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (type not found)**

Run: `dotnet test tests/Arrr.Tests/Arrr.Tests.csproj --filter "DBusNotifySubscriber" -v n`

Expected: compile error — `DBusNotifySubscriber` does not exist.

- [ ] **Step 3: Implement DBusNotifySubscriber**

Create `src/Arrr.Service/Subscribers/DBusNotifySubscriber.cs`:

```csharp
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.DBus;
using Serilog;
using Tmds.DBus;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Subscribers;

internal class DBusNotifySubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly ILogger _logger = Log.ForContext<DBusNotifySubscriber>();
    private Connection? _connection;

    public DBusNotifySubscriber(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _connection = new Connection(Address.Session);
            await _connection.ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "D-Bus session bus unavailable — desktop notifications disabled");
            _connection = null;
            return;
        }

        var proxy = _connection.CreateProxy<INotifications>(
            "org.freedesktop.Notifications",
            "/org/freedesktop/Notifications"
        );

        _eventBus.Subscribe<Notification>(async (notification, ct) =>
        {
            try
            {
                await proxy.NotifyAsync(
                    notification.Source,
                    0,
                    notification.IconUrl ?? "",
                    notification.Title,
                    notification.Body,
                    [],
                    new Dictionary<string, object>(),
                    -1
                );
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send D-Bus notification: {Title}", notification.Title);
            }
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _connection?.Dispose();
        _connection = null;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Arrr.Tests/Arrr.Tests.csproj --filter "DBusNotifySubscriber" -v n`

Expected: 2 tests pass.

- [ ] **Step 5: Run all tests to check for regressions**

Run: `dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -v n`

Expected: all tests pass, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add src/Arrr.Service/Subscribers/DBusNotifySubscriber.cs tests/Arrr.Tests/Service/DBusNotifySubscriberTests.cs
git commit -m "feat(dbus): implement DBusNotifySubscriber IHostedService"
```

---

### Task 3: Register DBusNotifySubscriber in Program.cs

**Files:**
- Modify: `src/Arrr.Service/Program.cs`

- [ ] **Step 1: Add AddHostedService registration**

In `src/Arrr.Service/Program.cs`, add after the existing `AddHostedService<PluginOrchestrator>()` line:

```csharp
builder.Services.AddHostedService<DBusNotifySubscriber>();
```

The relevant block becomes:

```csharp
builder.Services.AddHostedService<EventBusHostedService>();
builder.Services.AddHostedService<PluginOrchestrator>();
builder.Services.AddHostedService<DBusNotifySubscriber>();
```

- [ ] **Step 2: Add using directive for DBusNotifySubscriber**

`DBusNotifySubscriber` is in `Arrr.Service.Subscribers` — already covered by the existing `using Arrr.Service.Subscribers;` in Program.cs. No new using needed.

Verify the top of Program.cs contains:

```csharp
using Arrr.Service.Subscribers;
```

- [ ] **Step 3: Build full solution**

Run: `dotnet build Arrr.sln`

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 4: Run all tests**

Run: `dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -v n`

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Arrr.Service/Program.cs
git commit -m "feat(dbus): register DBusNotifySubscriber in service host"
```
