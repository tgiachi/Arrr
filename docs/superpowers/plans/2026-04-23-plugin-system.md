# Plugin System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare il sistema plugin completo di Arrr con contratti estesi, event bus in-process, hot-reload via AssemblyLoadContext, log per-plugin, config per-plugin e HTTP callback endpoint.

**Architecture:** I plugin pubblicano eventi su un `IEventBus` centrale (Channel-based) invece di scrivere su un ChannelWriter diretto. Il `PluginOrchestrator` sostituisce il `Worker`, gestisce il ciclo di vita dei plugin con `FileSystemWatcher` + `AssemblyLoadContext(isCollectible:true)` per hot-reload. ASP.NET Minimal API espone `/callback/{pluginName}` per OAuth e flussi HTTP.

**Tech Stack:** .NET 10, NUnit 4.5, Serilog, ASP.NET Core Minimal API, `System.Threading.Channels`, `System.Runtime.Loader.AssemblyLoadContext`

---

## File Map

### Arrr.Core — nuovi
| File | Responsabilità |
|------|---------------|
| `Interfaces/IArrrEvent.cs` | Contratto base per tutti gli eventi del bus |
| `Interfaces/IEventBus.cs` | Publish/Subscribe per IArrrEvent |
| `Interfaces/ISourcePlugin.cs` | Esteso con Id, Version, Author, Description, Categories |
| `Interfaces/IPluginContext.cs` | Contesto iniettato al plugin: ConfigPath, Logger, CallbackUrl, EventBus |
| `Interfaces/IPluginRegistry.cs` | Registro dei plugin attivi, lookup per callback |
| `Services/EventBusService.cs` | Implementazione IEventBus con Channel<IArrrEvent> |
| `Data/Config/PluginEntry.cs` | DTO: Id, Name, Enabled |

### Arrr.Core — modificati
| File | Modifica |
|------|---------|
| `Data/Notifications/Notification.cs` | Implementa IArrrEvent |
| `Data/Config/ArrrConfig.cs` | Aggiunge HttpPort, List<PluginEntry> Plugins |
| `Data/Config/ArrrConfigJsonContext.cs` | Registra PluginEntry |
| `Types/DirectoryType.cs` | Aggiunge Configs |

### Arrr.Service — nuovi
| File | Responsabilità |
|------|---------------|
| `Interfaces/IHttpCallbackPlugin.cs` | Plugin opzionale con HandleCallbackAsync (usa HttpContext) |
| `Services/PluginRegistryService.cs` | Implementazione IPluginRegistry |
| `Services/EventBusHostedService.cs` | Wrap IHostedService per EventBusService |
| `Internal/PluginLoadContext.cs` | AssemblyLoadContext(isCollectible:true) per plugin |
| `Internal/PluginHost.cs` | Stato runtime: Plugin + LoadContext + Cts + RunTask |
| `Internal/PluginContextFactory.cs` | Crea IPluginContext per ogni plugin |
| `Internal/PluginOrchestrator.cs` | IHostedService: scan + FileSystemWatcher + hot-reload |
| `Subscribers/SocketBroadcastSubscriber.cs` | Subscribe<Notification> → UnixSocketServer.BroadcastAsync |

### Arrr.Service — modificati
| File | Modifica |
|------|---------|
| `Program.cs` | WebApplication, registrazioni nuovi servizi, HTTP port |
| `Internal/UnixSocketServer.cs` | Espone BroadcastAsync pubblico |

### Arrr.Service — rimossi
| File | Motivo |
|------|--------|
| `Worker.cs` | Sostituito da PluginOrchestrator |

### Arrr.Tests — nuovi
| File | Soggetto |
|------|---------|
| `Core/EventBusServiceTests.cs` | EventBusService publish/subscribe/dispatch |
| `Service/PluginRegistryServiceTests.cs` | Register/Unregister/TryGetCallback |
| `Service/PluginContextFactoryTests.cs` | Percorsi generati correttamente |

---

## Task 1: IArrrEvent + Notification aggiornata

**Files:**
- Create: `src/Arrr.Core/Interfaces/IArrrEvent.cs`
- Modify: `src/Arrr.Core/Data/Notifications/Notification.cs`
- Test: `tests/Arrr.Tests/Core/EventBusServiceTests.cs` (scaffolding iniziale)

- [ ] **Step 1: Crea `IArrrEvent`**

```csharp
// src/Arrr.Core/Interfaces/IArrrEvent.cs
namespace Arrr.Core.Interfaces;

/// <summary>Base contract for all events published on the Arrr event bus.</summary>
public interface IArrrEvent
{
    DateTimeOffset Timestamp { get; }
}
```

- [ ] **Step 2: Aggiorna `Notification` per implementare `IArrrEvent`**

```csharp
// src/Arrr.Core/Data/Notifications/Notification.cs
using Arrr.Core.Interfaces;

namespace Arrr.Core.Data.Notifications;

public record Notification(
    Guid Id,
    string Source,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    string? IconUrl
) : IArrrEvent;
```

- [ ] **Step 3: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/Arrr.Core/Interfaces/IArrrEvent.cs src/Arrr.Core/Data/Notifications/Notification.cs
git commit -m "feat(core): add IArrrEvent interface and make Notification implement it"
```

---

## Task 2: IEventBus + ISourcePlugin esteso

**Files:**
- Create: `src/Arrr.Core/Interfaces/IEventBus.cs`
- Modify: `src/Arrr.Core/Interfaces/ISourcePlugin.cs`

- [ ] **Step 1: Crea `IEventBus`**

```csharp
// src/Arrr.Core/Interfaces/IEventBus.cs
namespace Arrr.Core.Interfaces;

/// <summary>In-process event bus for publishing and subscribing to Arrr events.</summary>
public interface IEventBus
{
    /// <summary>Publishes an event to all registered subscribers of type <typeparamref name="T"/>.</summary>
    Task PublishAsync<T>(T evt, CancellationToken ct = default) where T : IArrrEvent;

    /// <summary>Registers a handler invoked for every event of type <typeparamref name="T"/>.</summary>
    void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IArrrEvent;
}
```

- [ ] **Step 2: Aggiorna `ISourcePlugin`**

```csharp
// src/Arrr.Core/Interfaces/ISourcePlugin.cs
namespace Arrr.Core.Interfaces;

/// <summary>
/// Contract for notification source plugins.
/// Each plugin connects to an external source and publishes notifications via IPluginContext.EventBus.
/// </summary>
public interface ISourcePlugin
{
    /// <summary>Reverse-domain unique identifier (e.g. com.github.tgiachi.arrr.plugins.rss).</summary>
    string Id { get; }

    /// <summary>Display name of the plugin.</summary>
    string Name { get; }

    /// <summary>Semantic version string (e.g. "1.0.0").</summary>
    string Version { get; }

    /// <summary>Author name or organization.</summary>
    string Author { get; }

    /// <summary>Short description of what this plugin does.</summary>
    string Description { get; }

    /// <summary>Category tags (e.g. ["social", "messaging"]).</summary>
    string[] Categories { get; }

    /// <summary>Icon identifier or path for UI display.</summary>
    string Icon { get; }

    /// <summary>
    /// Starts the plugin. The plugin publishes events via <paramref name="context"/>.EventBus
    /// until cancellation is requested.
    /// </summary>
    Task StartAsync(IPluginContext context, CancellationToken ct);
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded.` (PluginRunner/PluginLoader avranno errori — li sistemiamo nei task successivi)

- [ ] **Step 4: Commit**

```bash
git add src/Arrr.Core/Interfaces/IEventBus.cs src/Arrr.Core/Interfaces/ISourcePlugin.cs
git commit -m "feat(core): add IEventBus interface and extend ISourcePlugin with metadata"
```

---

## Task 3: IPluginContext + IPluginRegistry

**Files:**
- Create: `src/Arrr.Core/Interfaces/IPluginContext.cs`
- Create: `src/Arrr.Core/Interfaces/IPluginRegistry.cs`

- [ ] **Step 1: Crea `IPluginContext`**

```csharp
// src/Arrr.Core/Interfaces/IPluginContext.cs
using Microsoft.Extensions.Logging;

namespace Arrr.Core.Interfaces;

/// <summary>Runtime context injected into each plugin at startup.</summary>
public interface IPluginContext
{
    /// <summary>Path to the plugin's dedicated config file ({pluginId}.config).</summary>
    string ConfigPath { get; }

    /// <summary>Logger scoped to this plugin, writing to logs/plugins/{pluginId}.log.</summary>
    ILogger Logger { get; }

    /// <summary>HTTP callback URL for this plugin (/callback/{pluginName}).</summary>
    string CallbackUrl { get; }

    /// <summary>Event bus for publishing notifications and other events.</summary>
    IEventBus EventBus { get; }
}
```

- [ ] **Step 2: Crea `IPluginRegistry`**

```csharp
// src/Arrr.Core/Interfaces/IPluginRegistry.cs
namespace Arrr.Core.Interfaces;

/// <summary>Registry of currently active plugins, used for HTTP callback dispatch.</summary>
public interface IPluginRegistry
{
    /// <summary>Registers a plugin as active.</summary>
    void Register(ISourcePlugin plugin);

    /// <summary>Removes a plugin by its Id.</summary>
    void Unregister(string pluginId);

    /// <summary>Returns all active plugins.</summary>
    IReadOnlyList<ISourcePlugin> GetAll();
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/Arrr.Core/Interfaces/IPluginContext.cs src/Arrr.Core/Interfaces/IPluginRegistry.cs
git commit -m "feat(core): add IPluginContext and IPluginRegistry interfaces"
```

---

## Task 4: Config models + DirectoryType

**Files:**
- Create: `src/Arrr.Core/Data/Config/PluginEntry.cs`
- Modify: `src/Arrr.Core/Data/Config/ArrrConfig.cs`
- Modify: `src/Arrr.Core/Data/Config/ArrrConfigJsonContext.cs`
- Modify: `src/Arrr.Core/Types/DirectoryType.cs`

- [ ] **Step 1: Crea `PluginEntry`**

```csharp
// src/Arrr.Core/Data/Config/PluginEntry.cs
namespace Arrr.Core.Data.Config;

public class PluginEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
```

- [ ] **Step 2: Aggiorna `ArrrConfig`**

```csharp
// src/Arrr.Core/Data/Config/ArrrConfig.cs
namespace Arrr.Core.Data.Config;

public class ArrrConfig
{
    public string SocketPath { get; set; } = "/tmp/arrr.sock";
    public int HttpPort { get; set; } = 5150;
    public List<PluginEntry> Plugins { get; set; } = [];
}
```

- [ ] **Step 3: Aggiorna `ArrrConfigJsonContext` per registrare `PluginEntry`**

```csharp
// src/Arrr.Core/Data/Config/ArrrConfigJsonContext.cs
using System.Text.Json.Serialization;

namespace Arrr.Core.Data.Config;

[JsonSerializable(typeof(ArrrConfig))]
[JsonSerializable(typeof(PluginEntry))]
public partial class ArrrConfigJsonContext : JsonSerializerContext
{
}
```

- [ ] **Step 4: Aggiunge `Configs` a `DirectoryType`**

```csharp
// src/Arrr.Core/Types/DirectoryType.cs
namespace Arrr.Core.Types;

public enum DirectoryType
{
    Scripts,
    Logs,
    Plugins,
    Configs
}
```

- [ ] **Step 5: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/Arrr.Core/Data/Config/PluginEntry.cs \
        src/Arrr.Core/Data/Config/ArrrConfig.cs \
        src/Arrr.Core/Data/Config/ArrrConfigJsonContext.cs \
        src/Arrr.Core/Types/DirectoryType.cs
git commit -m "feat(core): add PluginEntry, update ArrrConfig with HttpPort and Plugins, add DirectoryType.Configs"
```

---

## Task 5: EventBusService + test

**Files:**
- Create: `src/Arrr.Core/Services/EventBusService.cs`
- Create: `tests/Arrr.Tests/Core/EventBusServiceTests.cs`

- [ ] **Step 1: Scrivi il test**

```csharp
// tests/Arrr.Tests/Core/EventBusServiceTests.cs
using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;

namespace Arrr.Tests.Core;

[TestFixture]
public class EventBusServiceTests
{
    private EventBusService _bus = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public async Task SetUp()
    {
        _bus = new EventBusService();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await _bus.StartAsync(_cts.Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _bus.StopAsync(_cts.Token);
        _cts.Dispose();
    }

    [Test]
    public async Task PublishAsync_WhenSubscriberRegistered_HandlerInvoked()
    {
        var received = new TaskCompletionSource<Notification>();
        _bus.Subscribe<Notification>((n, ct) =>
        {
            received.TrySetResult(n);
            return Task.CompletedTask;
        });

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(notification, _cts.Token);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(result, Is.EqualTo(notification));
    }

    [Test]
    public async Task PublishAsync_WhenMultipleSubscribers_AllHandlersInvoked()
    {
        var count = 0;
        _bus.Subscribe<Notification>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        _bus.Subscribe<Notification>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        var notification = new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(notification, _cts.Token);

        await Task.Delay(100, _cts.Token);
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task PublishAsync_WhenNoSubscribers_DoesNotThrow()
    {
        var notification = new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null);
        Assert.DoesNotThrowAsync(() => _bus.PublishAsync(notification, _cts.Token));
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Esegui il test — deve fallire**

```bash
dotnet test Arrr.slnx --filter "FullyQualifiedName~EventBusServiceTests" 2>&1 | tail -5
```
Expected: errore compilazione (`EventBusService` non esiste)

- [ ] **Step 3: Implementa `EventBusService`**

```csharp
// src/Arrr.Core/Services/EventBusService.cs
using System.Threading.Channels;
using Arrr.Core.Interfaces;

namespace Arrr.Core.Services;

public class EventBusService : IEventBus
{
    private readonly Channel<IArrrEvent> _channel;
    private readonly List<(Type EventType, Func<IArrrEvent, CancellationToken, Task> Handler)> _subscribers = [];
    private readonly Lock _lock = new();

    private CancellationTokenSource _cts = new();
    private Task _dispatchTask = Task.CompletedTask;

    public EventBusService()
    {
        _channel = Channel.CreateUnbounded<IArrrEvent>(new UnboundedChannelOptions { SingleReader = true });
    }

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _dispatchTask = Task.Run(() => DispatchLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync();

        try
        {
            await _dispatchTask.WaitAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    public async Task PublishAsync<T>(T evt, CancellationToken ct = default) where T : IArrrEvent
    {
        await _channel.Writer.WriteAsync(evt, ct);
    }

    public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IArrrEvent
    {
        lock (_lock)
        {
            _subscribers.Add((typeof(T), (evt, ct) => handler((T)evt, ct)));
        }
    }

    private async Task DispatchLoopAsync(CancellationToken ct)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            List<Func<IArrrEvent, CancellationToken, Task>> handlers;

            lock (_lock)
            {
                handlers = _subscribers
                    .Where(s => s.EventType.IsAssignableFrom(evt.GetType()))
                    .Select(s => s.Handler)
                    .ToList();
            }

            foreach (var handler in handlers)
            {
                try
                {
                    await handler(evt, ct);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[EventBus] Handler error: {ex.Message}");
                }
            }
        }
    }
}
```

- [ ] **Step 4: Esegui i test — devono passare**

```bash
dotnet test Arrr.slnx --filter "FullyQualifiedName~EventBusServiceTests" --logger "console;verbosity=normal" 2>&1 | tail -10
```
Expected: `Passed: 3`

- [ ] **Step 5: Commit**

```bash
git add src/Arrr.Core/Services/EventBusService.cs tests/Arrr.Tests/Core/EventBusServiceTests.cs
git commit -m "feat(core): implement EventBusService with Channel-based dispatch loop"
```

---

## Task 6: PluginRegistryService + test

**Files:**
- Create: `src/Arrr.Service/Services/PluginRegistryService.cs`
- Create: `src/Arrr.Service/Interfaces/IHttpCallbackPlugin.cs`
- Create: `tests/Arrr.Tests/Service/PluginRegistryServiceTests.cs`

- [ ] **Step 1: Crea `IHttpCallbackPlugin` in Service**

```csharp
// src/Arrr.Service/Interfaces/IHttpCallbackPlugin.cs
using Arrr.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Arrr.Service.Interfaces;

/// <summary>
/// Optional interface for plugins that need to handle HTTP callbacks (e.g. OAuth flows).
/// Implement this alongside ISourcePlugin to receive requests at /callback/{pluginName}.
/// </summary>
public interface IHttpCallbackPlugin : ISourcePlugin
{
    /// <summary>Handles an incoming HTTP request on the plugin's callback URL.</summary>
    Task HandleCallbackAsync(HttpContext httpContext, CancellationToken ct);
}
```

- [ ] **Step 2: Scrivi il test**

```csharp
// tests/Arrr.Tests/Service/PluginRegistryServiceTests.cs
using Arrr.Core.Interfaces;
using Arrr.Service.Services;
using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class PluginRegistryServiceTests
{
    private PluginRegistryService _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new PluginRegistryService();
    }

    [Test]
    public void Register_WhenPluginAdded_AppearsInGetAll()
    {
        var plugin = new FakeSourcePlugin("com.test.plugin");

        _registry.Register(plugin);

        Assert.That(_registry.GetAll(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Unregister_WhenPluginRemoved_DisappearsFromGetAll()
    {
        var plugin = new FakeSourcePlugin("com.test.plugin");
        _registry.Register(plugin);

        _registry.Unregister("com.test.plugin");

        Assert.That(_registry.GetAll(), Is.Empty);
    }

    [Test]
    public void GetAll_WhenEmpty_ReturnsEmptyList()
    {
        Assert.That(_registry.GetAll(), Is.Empty);
    }
}
```

- [ ] **Step 3: Esegui — deve fallire**

```bash
dotnet test Arrr.slnx --filter "FullyQualifiedName~PluginRegistryServiceTests" 2>&1 | tail -5
```
Expected: errore compilazione

- [ ] **Step 4: Aggiorna `FakeSourcePlugin` con `Id`**

Il `FakeSourcePlugin` in `tests/Arrr.Tests/Support/FakeSourcePlugin.cs` va aggiornato per i nuovi contratti `ISourcePlugin`:

```csharp
// tests/Arrr.Tests/Support/FakeSourcePlugin.cs
using System.Threading.Channels;
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
            throw _throws;

        foreach (var n in _notifications)
            await context.EventBus.PublishAsync(n, ct);
    }
}
```

- [ ] **Step 5: Implementa `PluginRegistryService`**

```csharp
// src/Arrr.Service/Services/PluginRegistryService.cs
using Arrr.Core.Interfaces;

namespace Arrr.Service.Services;

public class PluginRegistryService : IPluginRegistry
{
    private readonly Dictionary<string, ISourcePlugin> _plugins = new();
    private readonly Lock _lock = new();

    public void Register(ISourcePlugin plugin)
    {
        lock (_lock)
        {
            _plugins[plugin.Id] = plugin;
        }
    }

    public void Unregister(string pluginId)
    {
        lock (_lock)
        {
            _plugins.Remove(pluginId);
        }
    }

    public IReadOnlyList<ISourcePlugin> GetAll()
    {
        lock (_lock)
        {
            return _plugins.Values.ToList();
        }
    }
}
```

- [ ] **Step 6: Esegui — devono passare**

```bash
dotnet test Arrr.slnx --filter "FullyQualifiedName~PluginRegistryServiceTests" --logger "console;verbosity=normal" 2>&1 | tail -8
```
Expected: `Passed: 3`

- [ ] **Step 7: Commit**

```bash
git add src/Arrr.Service/Interfaces/IHttpCallbackPlugin.cs \
        src/Arrr.Service/Services/PluginRegistryService.cs \
        tests/Arrr.Tests/Service/PluginRegistryServiceTests.cs \
        tests/Arrr.Tests/Support/FakeSourcePlugin.cs
git commit -m "feat(service): add IHttpCallbackPlugin, PluginRegistryService; update FakeSourcePlugin for new ISourcePlugin contract"
```

---

## Task 7: EventBusHostedService

**Files:**
- Create: `src/Arrr.Service/Services/EventBusHostedService.cs`

- [ ] **Step 1: Implementa `EventBusHostedService`**

```csharp
// src/Arrr.Service/Services/EventBusHostedService.cs
using Arrr.Core.Services;

namespace Arrr.Service.Services;

public class EventBusHostedService : IHostedService
{
    private readonly EventBusService _eventBusService;

    public EventBusHostedService(EventBusService eventBusService)
    {
        _eventBusService = eventBusService;
    }

    public Task StartAsync(CancellationToken ct)
    {
        return _eventBusService.StartAsync(ct);
    }

    public Task StopAsync(CancellationToken ct)
    {
        return _eventBusService.StopAsync(ct);
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/Arrr.Service/Services/EventBusHostedService.cs
git commit -m "feat(service): add EventBusHostedService as IHostedService wrapper for EventBusService"
```

---

## Task 8: Plugin internals (LoadContext, Host, ContextFactory)

**Files:**
- Create: `src/Arrr.Service/Internal/PluginLoadContext.cs`
- Create: `src/Arrr.Service/Internal/PluginHost.cs`
- Create: `src/Arrr.Service/Internal/PluginContextFactory.cs`
- Create: `tests/Arrr.Tests/Service/PluginContextFactoryTests.cs`

- [ ] **Step 1: Crea `PluginLoadContext`**

```csharp
// src/Arrr.Service/Internal/PluginLoadContext.cs
using System.Reflection;
using System.Runtime.Loader;

namespace Arrr.Service.Internal;

internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
```

- [ ] **Step 2: Crea `PluginHost`**

```csharp
// src/Arrr.Service/Internal/PluginHost.cs
using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal class PluginHost
{
    private readonly ISourcePlugin _plugin;
    private readonly PluginLoadContext _loadContext;
    private readonly CancellationTokenSource _cts;
    private readonly Task _runTask;

    public ISourcePlugin Plugin => _plugin;
    public string PluginId => _plugin.Id;

    public PluginHost(ISourcePlugin plugin, PluginLoadContext loadContext, CancellationTokenSource cts, Task runTask)
    {
        _plugin = plugin;
        _loadContext = loadContext;
        _cts = cts;
        _runTask = runTask;
    }

    public async Task StopAsync()
    {
        await _cts.CancelAsync();

        try
        {
            await _runTask;
        }
        catch (OperationCanceledException) { }

        _cts.Dispose();
        _loadContext.Unload();
    }
}
```

- [ ] **Step 3: Scrivi test per `PluginContextFactory`**

```csharp
// tests/Arrr.Tests/Service/PluginContextFactoryTests.cs
using Arrr.Core.Directories;
using Arrr.Core.Services;
using Arrr.Core.Types;
using Arrr.Service.Internal;
using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class PluginContextFactoryTests
{
    private string _tempRoot = "";
    private DirectoriesConfig _directoriesConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"arrr_ctx_test_{Guid.NewGuid()}");
        _directoriesConfig = new DirectoriesConfig(_tempRoot, Enum.GetNames<DirectoryType>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void Create_ConfigPath_PointsToPluginIdFileInConfigsDir()
    {
        var factory = new PluginContextFactory(new EventBusService(), _directoriesConfig);
        var plugin = new FakeSourcePlugin("com.test.arrr.plugins.rss");

        var ctx = factory.Create(plugin);

        var expected = Path.Combine(_directoriesConfig[DirectoryType.Configs], "com.test.arrr.plugins.rss.config");
        Assert.That(ctx.ConfigPath, Is.EqualTo(expected));
    }

    [Test]
    public void Create_CallbackUrl_ContainsPluginName()
    {
        var factory = new PluginContextFactory(new EventBusService(), _directoriesConfig);
        var plugin = new FakeSourcePlugin("com.test.arrr.plugins.rss");

        var ctx = factory.Create(plugin);

        Assert.That(ctx.CallbackUrl, Does.Contain("rss"));
    }
}
```

- [ ] **Step 4: Esegui test — deve fallire**

```bash
dotnet test Arrr.slnx --filter "FullyQualifiedName~PluginContextFactoryTests" 2>&1 | tail -5
```
Expected: errore compilazione (`PluginContextFactory` non esiste)

- [ ] **Step 5: Implementa `PluginContextFactory`**

```csharp
// src/Arrr.Service/Internal/PluginContextFactory.cs
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Services;
using Arrr.Core.Types;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace Arrr.Service.Internal;

internal class PluginContextFactory
{
    private readonly IEventBus _eventBus;
    private readonly DirectoriesConfig _directoriesConfig;

    public PluginContextFactory(IEventBus eventBus, DirectoriesConfig directoriesConfig)
    {
        _eventBus = eventBus;
        _directoriesConfig = directoriesConfig;
    }

    public IPluginContext Create(ISourcePlugin plugin)
    {
        var configPath = Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{plugin.Id}.config");
        var logPath = Path.Combine(_directoriesConfig[DirectoryType.Logs], "plugins", $"{plugin.Id}.log");
        var callbackUrl = $"/callback/{plugin.Name}";

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var logger = new SerilogLoggerFactory(serilogLogger).CreateLogger(plugin.Id);

        return new PluginContext(configPath, logger, callbackUrl, _eventBus);
    }
}

internal sealed class PluginContext : IPluginContext
{
    public string ConfigPath { get; }
    public ILogger Logger { get; }
    public string CallbackUrl { get; }
    public IEventBus EventBus { get; }

    public PluginContext(string configPath, ILogger logger, string callbackUrl, IEventBus eventBus)
    {
        ConfigPath = configPath;
        Logger = logger;
        CallbackUrl = callbackUrl;
        EventBus = eventBus;
    }
}
```

- [ ] **Step 6: Esegui test — devono passare**

```bash
dotnet test Arrr.slnx --filter "FullyQualifiedName~PluginContextFactoryTests" --logger "console;verbosity=normal" 2>&1 | tail -8
```
Expected: `Passed: 2`

- [ ] **Step 7: Commit**

```bash
git add src/Arrr.Service/Internal/PluginLoadContext.cs \
        src/Arrr.Service/Internal/PluginHost.cs \
        src/Arrr.Service/Internal/PluginContextFactory.cs \
        tests/Arrr.Tests/Service/PluginContextFactoryTests.cs
git commit -m "feat(service): add PluginLoadContext, PluginHost, PluginContextFactory with per-plugin logging and config path"
```

---

## Task 9: PluginOrchestrator (hot-reload)

**Files:**
- Create: `src/Arrr.Service/Internal/PluginOrchestrator.cs`

- [ ] **Step 1: Implementa `PluginOrchestrator`**

```csharp
// src/Arrr.Service/Internal/PluginOrchestrator.cs
using System.Reflection;
using Arrr.Core.Data.Config;
using Arrr.Core.Interfaces;
using Arrr.Service.Services;

namespace Arrr.Service.Internal;

internal class PluginOrchestrator : BackgroundService
{
    private readonly ILogger<PluginOrchestrator> _logger;
    private readonly PluginContextFactory _contextFactory;
    private readonly IPluginRegistry _registry;
    private readonly ArrrConfig _config;
    private readonly string _pluginsPath;

    private readonly Dictionary<string, PluginHost> _hosts = new();
    private FileSystemWatcher? _watcher;

    public PluginOrchestrator(
        ILogger<PluginOrchestrator> logger,
        PluginContextFactory contextFactory,
        IPluginRegistry registry,
        IConfigService configService,
        Arrr.Core.Directories.DirectoriesConfig directoriesConfig)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _registry = registry;
        _config = configService.Config;
        _pluginsPath = directoriesConfig[Arrr.Core.Types.DirectoryType.Plugins];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadAllPluginsAsync(stoppingToken);
        StartWatcher(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        await StopAllPluginsAsync();
    }

    private async Task LoadAllPluginsAsync(CancellationToken ct)
    {
        foreach (var file in Directory.EnumerateFiles(_pluginsPath, "*.dll"))
            await TryLoadPluginAsync(file, ct);
    }

    private async Task TryLoadPluginAsync(string dllPath, CancellationToken ct)
    {
        try
        {
            var context = new PluginLoadContext(dllPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

            if (pluginType is null)
            {
                context.Unload();
                return;
            }

            if (Activator.CreateInstance(pluginType) is not ISourcePlugin plugin)
            {
                context.Unload();
                return;
            }

            var entry = _config.Plugins.FirstOrDefault(p => p.Id == plugin.Id);
            if (entry is null || !entry.Enabled)
            {
                _logger.LogDebug("Plugin {Id} not in config or disabled, skipping", plugin.Id);
                context.Unload();
                return;
            }

            await StartPluginAsync(plugin, context, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {Path}", dllPath);
        }
    }

    private async Task StartPluginAsync(ISourcePlugin plugin, PluginLoadContext loadContext, CancellationToken ct)
    {
        if (_hosts.TryGetValue(plugin.Id, out var existing))
            await existing.StopAsync();

        var pluginCtx = _contextFactory.Create(plugin);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var runTask = Task.Run(async () =>
        {
            try
            {
                await plugin.StartAsync(pluginCtx, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plugin {Id} crashed", plugin.Id);
            }
        }, cts.Token);

        var host = new PluginHost(plugin, loadContext, cts, runTask);
        _hosts[plugin.Id] = host;
        _registry.Register(plugin);

        _logger.LogInformation("Started plugin: {Id} v{Version}", plugin.Id, plugin.Version);
    }

    private void StartWatcher(CancellationToken ct)
    {
        _watcher = new FileSystemWatcher(_pluginsPath, "*.dll")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, e) => _ = TryLoadPluginAsync(e.FullPath, ct);
        _watcher.Changed += (_, e) => _ = TryLoadPluginAsync(e.FullPath, ct);
        _watcher.Deleted += (_, e) => _ = UnloadPluginByPathAsync(e.FullPath);
    }

    private async Task UnloadPluginByPathAsync(string dllPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(dllPath);
        var host = _hosts.Values.FirstOrDefault(h => h.PluginId.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (host is null) return;

        await host.StopAsync();
        _hosts.Remove(host.PluginId);
        _registry.Unregister(host.PluginId);
        _logger.LogInformation("Unloaded plugin: {Id}", host.PluginId);
    }

    private async Task StopAllPluginsAsync()
    {
        foreach (var host in _hosts.Values)
            await host.StopAsync();
        _hosts.Clear();
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/Arrr.Service/Internal/PluginOrchestrator.cs
git commit -m "feat(service): add PluginOrchestrator with FileSystemWatcher hot-reload and AssemblyLoadContext isolation"
```

---

## Task 10: SocketBroadcastSubscriber + refactor UnixSocketServer

**Files:**
- Modify: `src/Arrr.Service/Internal/UnixSocketServer.cs`
- Create: `src/Arrr.Service/Subscribers/SocketBroadcastSubscriber.cs`

- [ ] **Step 1: Refactor `UnixSocketServer` — rimuovi `ChannelReader`, aggiungi `BroadcastAsync` pubblico**

Il broadcasting avviene ora via event bus subscriber, non più via canale interno. Riscrivi `UnixSocketServer`:

```csharp
// src/Arrr.Service/Internal/UnixSocketServer.cs
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;

namespace Arrr.Service.Internal;

/// <summary>
/// Listens on a Unix domain socket and broadcasts incoming <see cref="Notification"/> entries
/// as newline-delimited JSON to all connected clients.
/// </summary>
internal class UnixSocketServer : IAsyncDisposable
{
    private readonly ILogger<UnixSocketServer> _logger;
    private readonly string _socketPath;
    private readonly List<NetworkStream> _clients = new();
    private readonly SemaphoreSlim _clientsLock = new(1, 1);

    private Socket? _listener;

    /// <summary>Initializes the server with the socket path.</summary>
    public UnixSocketServer(ILogger<UnixSocketServer> logger, string socketPath)
    {
        _logger = logger;
        _socketPath = socketPath;
    }

    /// <summary>Starts accepting clients until cancellation is requested.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(10);

        _logger.LogInformation("Arrr service started, listening on {Path}", _socketPath);

        await AcceptClientsAsync(ct);
    }

    /// <summary>Broadcasts a notification as JSON to all connected clients.</summary>
    public async Task BroadcastAsync(Notification notification, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(notification) + "\n";
        var bytes = Encoding.UTF8.GetBytes(json);

        await _clientsLock.WaitAsync(ct);
        var dead = new List<NetworkStream>();

        foreach (var client in _clients)
        {
            try { await client.WriteAsync(bytes, ct); }
            catch { dead.Add(client); }
        }

        foreach (var d in dead)
        {
            _clients.Remove(d);
            await d.DisposeAsync();
        }

        _clientsLock.Release();
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listener!.AcceptAsync(ct);
                var stream = new NetworkStream(clientSocket, true);

                await _clientsLock.WaitAsync(ct);
                _clients.Add(stream);
                _clientsLock.Release();

                _logger.LogDebug("Client connected, total: {Count}", _clients.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error accepting client"); }
        }
    }

    /// <summary>Closes all client connections, disposes the listener socket and removes the socket file.</summary>
    public async ValueTask DisposeAsync()
    {
        await _clientsLock.WaitAsync();
        foreach (var client in _clients)
            client.Dispose();
        _clients.Clear();
        _clientsLock.Release();

        _listener?.Dispose();

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
    }
}
```

- [ ] **Step 2: Crea `SocketBroadcastSubscriber`**

```csharp
// src/Arrr.Service/Subscribers/SocketBroadcastSubscriber.cs
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.Internal;

namespace Arrr.Service.Subscribers;

public class SocketBroadcastSubscriber
{
    public SocketBroadcastSubscriber(IEventBus eventBus, UnixSocketServer socketServer)
    {
        eventBus.Subscribe<Notification>(socketServer.BroadcastAsync);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/Arrr.Service/Internal/UnixSocketServer.cs \
        src/Arrr.Service/Subscribers/SocketBroadcastSubscriber.cs
git commit -m "feat(service): add SocketBroadcastSubscriber, expose BroadcastAsync on UnixSocketServer"
```

---

## Task 11: Program.cs — wiring finale + rimozione Worker

**Files:**
- Modify: `src/Arrr.Service/Program.cs`
- Delete: `src/Arrr.Service/Worker.cs`

- [ ] **Step 1: Aggiungi `Microsoft.AspNetCore.App` framework reference al csproj**

In `src/Arrr.Service/Arrr.Service.csproj`, aggiungi dentro `<ItemGroup>`:
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

- [ ] **Step 2: Riscrivi `Program.cs`**

```csharp
// src/Arrr.Service/Program.cs
using Arrr.Core.Data.Config;
using Arrr.Core.Directories;
using Arrr.Core.Extensions.Logger;
using Arrr.Core.Interfaces;
using Arrr.Core.Json;
using Arrr.Core.Services;
using Arrr.Core.Types;
using Arrr.Service.Internal;
using Arrr.Service.Interfaces;
using Arrr.Service.Services;
using Arrr.Service.Subscribers;
using ConsoleAppFramework;
using Serilog;

JsonUtils.RegisterJsonContext(ArrrConfigJsonContext.Default);

await ConsoleApp.RunAsync(
    args,
    async (
            string? rootDirectory = null,
            LogLevelType logLevelType = LogLevelType.Information,
            bool logToFile = true,
            CancellationToken ct = default
        ) =>
    {
        rootDirectory ??= Environment.CurrentDirectory;

        var directoriesConfig = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(logLevelType.ToSerilogLogLevel())
            .WriteTo.Console();

        if (logToFile)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.File(
                Path.Combine(directoriesConfig[DirectoryType.Logs], "log-.txt"),
                rollingInterval: RollingInterval.Day
            );
        }

        Log.Logger = loggerConfiguration.CreateLogger();
        Log.Logger.Information("Root directory: {RootDirectory}", rootDirectory);

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton(directoriesConfig);
        builder.Services.AddSingleton<IConfigService, ConfigService>();
        builder.Services.AddSingleton<EventBusService>();
        builder.Services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBusService>());
        builder.Services.AddSingleton<IPluginRegistry, PluginRegistryService>();
        builder.Services.AddSingleton<UnixSocketServer>(sp =>
        {
            var config = sp.GetRequiredService<IConfigService>().Config;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new UnixSocketServer(loggerFactory.CreateLogger<UnixSocketServer>(), config.SocketPath);
        });
        builder.Services.AddSingleton<SocketBroadcastSubscriber>();
        builder.Services.AddSingleton<PluginContextFactory>();
        builder.Services.AddHostedService<EventBusHostedService>();
        builder.Services.AddHostedService<PluginOrchestrator>();

        builder.Logging.ClearProviders().AddSerilog();

        var app = builder.Build();

        var configService = app.Services.GetRequiredService<IConfigService>();
        await configService.LoadAsync(ct);

        // Attiva il subscriber socket
        app.Services.GetRequiredService<SocketBroadcastSubscriber>();

        app.MapGet("/callback/{pluginName}", async (string pluginName, HttpContext ctx, IPluginRegistry registry) =>
        {
            var plugin = registry.GetAll()
                .OfType<IHttpCallbackPlugin>()
                .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

            if (plugin is null)
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            await plugin.HandleCallbackAsync(ctx, ct);
        });

        app.MapPost("/callback/{pluginName}", async (string pluginName, HttpContext ctx, IPluginRegistry registry) =>
        {
            var plugin = registry.GetAll()
                .OfType<IHttpCallbackPlugin>()
                .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

            if (plugin is null)
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            await plugin.HandleCallbackAsync(ctx, ct);
        });

        await app.RunAsync(ct);
    }
);
```

- [ ] **Step 3: Elimina `Worker.cs`**

```bash
rm src/Arrr.Service/Worker.cs
```

- [ ] **Step 4: Build**

```bash
dotnet build Arrr.slnx
```
Expected: `Build succeeded.`

- [ ] **Step 5: Esegui tutti i test**

```bash
dotnet test Arrr.slnx --logger "console;verbosity=normal" 2>&1 | tail -12
```
Expected: tutti i test passano (i test `UnixSocketServerTests` e `PluginRunnerTests` andranno aggiornati perché `ISourcePlugin` è cambiato)

- [ ] **Step 6: Commit**

```bash
git add src/Arrr.Service/Program.cs src/Arrr.Service/Arrr.Service.csproj
git rm src/Arrr.Service/Worker.cs
git commit -m "feat(service): migrate to WebApplication, wire all services, remove Worker in favor of PluginOrchestrator"
```

---

## Task 12: Fix test esistenti post-refactor

**Files:**
- Modify: `tests/Arrr.Tests/Service/PluginRunnerTests.cs`
- Modify: `tests/Arrr.Tests/Service/UnixSocketServerTests.cs`

- [ ] **Step 1: Aggiorna `PluginRunnerTests`**

`PluginRunner` è stato sostituito da `PluginOrchestrator`. I vecchi test di `PluginRunner` vanno rimossi o sostituiti con test che verificano che `FakeSourcePlugin.StartAsync` pubblichi sull'event bus:

```csharp
// tests/Arrr.Tests/Service/PluginRunnerTests.cs
using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;
using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class PluginRunnerTests
{
    [Test]
    public async Task StartAsync_WhenPluginPublishesNotification_EventBusReceivesIt()
    {
        var expected = new Notification(Guid.NewGuid(), "fake", "Title", "Body", DateTimeOffset.UtcNow, null);
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await bus.StartAsync(cts.Token);

        var received = new TaskCompletionSource<Notification>();
        bus.Subscribe<Notification>((n, _) => { received.TrySetResult(n); return Task.CompletedTask; });

        var plugin = new FakeSourcePlugin("com.test.plugin", [expected]);
        var ctx = new FakePluginContext(bus);
        await plugin.StartAsync(ctx, cts.Token);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(result, Is.EqualTo(expected));

        await bus.StopAsync(cts.Token);
    }

    [Test]
    public async Task StartAsync_WhenPluginThrows_ExceptionIsPlugin()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        await bus.StartAsync(cts.Token);

        var plugin = new FakeSourcePlugin("com.test.broken", throws: new InvalidOperationException("boom"));
        var ctx = new FakePluginContext(bus);

        Assert.ThrowsAsync<InvalidOperationException>(() => plugin.StartAsync(ctx, cts.Token));

        await bus.StopAsync(cts.Token);
    }
}
```

Aggiungi `FakePluginContext` in `tests/Arrr.Tests/Support/FakePluginContext.cs`:

```csharp
// tests/Arrr.Tests/Support/FakePluginContext.cs
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Support;

internal class FakePluginContext : IPluginContext
{
    public string ConfigPath => "/tmp/fake.config";
    public Microsoft.Extensions.Logging.ILogger Logger => NullLogger.Instance;
    public string CallbackUrl => "/callback/fake";
    public IEventBus EventBus { get; }

    public FakePluginContext(IEventBus eventBus)
    {
        EventBus = eventBus;
    }
}
```

- [ ] **Step 2: Esegui tutti i test**

```bash
dotnet test Arrr.slnx --logger "console;verbosity=normal" 2>&1 | tail -15
```
Expected: tutti passano

- [ ] **Step 3: Commit**

```bash
git add tests/Arrr.Tests/Service/PluginRunnerTests.cs \
        tests/Arrr.Tests/Support/FakePluginContext.cs
git commit -m "test: update PluginRunnerTests and add FakePluginContext for new IPluginContext contract"
```

---

## Verifica finale

```bash
# Build completo
dotnet build Arrr.slnx

# Tutti i test
dotnet test Arrr.slnx

# Avvio service
dotnet run --project src/Arrr.Service -- --root-directory /tmp/arrr-test --log-to-file false

# In altro terminale — verifica socket
socat - UNIX-CONNECT:/tmp/arrr.sock

# Verifica HTTP callback (deve rispondere 404 — no plugin attivo)
curl -v http://localhost:5150/callback/rss

# Verifica hot-reload: copia un plugin di test nella cartella
cp MyPlugin.dll /tmp/arrr-test/plugins/
# → log deve mostrare "Started plugin: com.xxx.xxx"
```
