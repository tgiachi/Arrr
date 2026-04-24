# Sink Plugin System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the output side of Arrr pluggable via `ISinkPlugin`, with D-Bus and Unix socket as built-in activatable sinks, mirroring the existing source plugin architecture.

**Architecture:** `SinkOrchestrator : BackgroundService, ISinkManager` subscribes once to `IEventBus` and fans out `Notification` to all running sinks. Built-in sinks (`DbusNotifySink`, `UnixSocketSink`) are always registered; external sinks load from `plugins/*.dll`. Old `DBusNotifySubscriber`, `SocketBroadcastSubscriber`, and `UnixSocketServer` are deleted.

**Tech Stack:** .NET 10, Tmds.DBus, NUnit 3, WebApplicationFactory (TestServer), existing `PluginLoadContext`, `EncryptionUtils`, `IConfigurablePlugin` patterns.

---

## File Map

**Create (Arrr.Core):**
- `src/Arrr.Core/Interfaces/ISinkPlugin.cs`
- `src/Arrr.Core/Interfaces/ISinkContext.cs`
- `src/Arrr.Core/Interfaces/ISinkManager.cs`
- `src/Arrr.Core/Data/Api/AvailableSinkResponse.cs`
- `src/Arrr.Core/Data/Config/SinkEntry.cs`

**Modify (Arrr.Core):**
- `src/Arrr.Core/Data/Config/ArrrConfig.cs` — add `List<SinkEntry> Sinks`
- `src/Arrr.Core/Data/Config/ArrrConfigJsonContext.cs` — add `SinkEntry`

**Create (Arrr.Service):**
- `src/Arrr.Service/Sinks/DbusNotifySink.cs`
- `src/Arrr.Service/Sinks/UnixSocketSink.cs`
- `src/Arrr.Service/Internal/SinkOrchestrator.cs` (+ inner `SinkContext` sealed class)
- `src/Arrr.Service/Api/SinksEndpoint.cs`

**Modify (Arrr.Service):**
- `src/Arrr.Service/Program.cs` — register `SinkOrchestrator`, remove old subscribers

**Delete (Arrr.Service):**
- `src/Arrr.Service/Subscribers/DBusNotifySubscriber.cs`
- `src/Arrr.Service/Subscribers/SocketBroadcastSubscriber.cs`
- `src/Arrr.Service/Internal/UnixSocketServer.cs`

**Create (Tests):**
- `tests/Arrr.Tests/Support/FakeSinkManager.cs`
- `tests/Arrr.Tests/Support/FakeSink.cs`
- `tests/Arrr.Tests/Support/FakeSinkContext.cs`
- `tests/Arrr.Tests/Sinks/DbusNotifySinkTests.cs`
- `tests/Arrr.Tests/Sinks/UnixSocketSinkTests.cs`
- `tests/Arrr.Tests/Service/SinksEndpointTests.cs`
- `tests/Arrr.Tests/Service/SinkOrchestratorTests.cs`

**Delete (Tests):**
- `tests/Arrr.Tests/Service/DBusNotifySubscriberTests.cs`
- `tests/Arrr.Tests/Service/UnixSocketServerTests.cs`

**Modify (UI):**
- `ui/src/types.ts`
- `ui/src/api.ts`
- `ui/src/App.tsx`

---

## Task 1: Core interfaces and data types

**Files:**
- Create: `src/Arrr.Core/Interfaces/ISinkPlugin.cs`
- Create: `src/Arrr.Core/Interfaces/ISinkContext.cs`
- Create: `src/Arrr.Core/Interfaces/ISinkManager.cs`
- Create: `src/Arrr.Core/Data/Api/AvailableSinkResponse.cs`
- Create: `src/Arrr.Core/Data/Config/SinkEntry.cs`
- Modify: `src/Arrr.Core/Data/Config/ArrrConfig.cs`
- Modify: `src/Arrr.Core/Data/Config/ArrrConfigJsonContext.cs`

- [ ] **Step 1: Create ISinkPlugin.cs**

```csharp
// src/Arrr.Core/Interfaces/ISinkPlugin.cs
using Arrr.Core.Data.Notifications;

namespace Arrr.Core.Interfaces;

/// <summary>Contract for notification sink plugins — output connectors that deliver notifications.</summary>
public interface ISinkPlugin
{
    string Id          { get; }
    string Name        { get; }
    string Version     { get; }
    string Author      { get; }
    string Description { get; }
    string Icon        { get; }

    Task StartAsync(ISinkContext context, CancellationToken ct);
    Task ConsumeAsync(Notification notification, CancellationToken ct);
    Task StopAsync();
}
```

- [ ] **Step 2: Create ISinkContext.cs**

```csharp
// src/Arrr.Core/Interfaces/ISinkContext.cs
using Microsoft.Extensions.Logging;

namespace Arrr.Core.Interfaces;

/// <summary>Runtime context injected into each sink at startup.</summary>
public interface ISinkContext
{
    string ConfigPath { get; }
    ILogger Logger    { get; }

    Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new();
    Task SaveConfigAsync<T>(T config, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create ISinkManager.cs**

```csharp
// src/Arrr.Core/Interfaces/ISinkManager.cs
using System.Text.Json;
using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>Manages sink lifecycle and exposes sink metadata for the REST API.</summary>
public interface ISinkManager
{
    IReadOnlyList<AvailableSinkResponse> GetAvailable();
    Task EnableAsync(string sinkId, CancellationToken ct);
    Task DisableAsync(string sinkId);
    Task ReloadAsync(string sinkId, CancellationToken ct);
    Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default);
    Task SaveSinkConfigAsync(string sinkId, JsonElement config, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create AvailableSinkResponse.cs**

```csharp
// src/Arrr.Core/Data/Api/AvailableSinkResponse.cs
namespace Arrr.Core.Data.Api;

public record AvailableSinkResponse(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string Icon,
    bool Enabled,
    bool Running,
    bool IsBuiltIn,
    bool HasConfig
);
```

- [ ] **Step 5: Create SinkEntry.cs**

```csharp
// src/Arrr.Core/Data/Config/SinkEntry.cs
namespace Arrr.Core.Data.Config;

public class SinkEntry
{
    public string Id      { get; set; } = "";
    public bool Enabled   { get; set; } = true;
}
```

- [ ] **Step 6: Add Sinks list to ArrrConfig**

Replace the full content of `src/Arrr.Core/Data/Config/ArrrConfig.cs`:

```csharp
namespace Arrr.Core.Data.Config;

public class ArrrConfig
{
    public string ApiKey  { get; set; } = "";
    public bool IsDebug   { get; set; } = false;
    public ArrrWebConfig Web { get; set; } = new();
    public List<PluginEntry> Plugins { get; set; } = [];
    public List<SinkEntry>   Sinks   { get; set; } = [];
}
```

Note: `SocketPath` is removed — it moves into `UnixSocketSink`'s own config file.

- [ ] **Step 7: Register SinkEntry in the JSON context**

Replace content of `src/Arrr.Core/Data/Config/ArrrConfigJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Arrr.Core.Data.Config;

[JsonSerializable(typeof(ArrrConfig))]
[JsonSerializable(typeof(PluginEntry))]
[JsonSerializable(typeof(SinkEntry))]
[JsonSerializable(typeof(ArrrWebConfig))]
public partial class ArrrConfigJsonContext : JsonSerializerContext { }
```

- [ ] **Step 8: Build Arrr.Core — zero errors**

```bash
dotnet build src/Arrr.Core/Arrr.Core.csproj -c Release
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Commit**

```bash
git add src/Arrr.Core/
git commit -m "feat(sink): add ISinkPlugin, ISinkContext, ISinkManager, AvailableSinkResponse, SinkEntry"
```

---

## Task 2: FakeSinkManager + SinksEndpoint (TDD)

**Files:**
- Create: `tests/Arrr.Tests/Support/FakeSinkManager.cs`
- Create: `tests/Arrr.Tests/Service/SinksEndpointTests.cs`
- Create: `src/Arrr.Service/Api/SinksEndpoint.cs`

- [ ] **Step 1: Create FakeSinkManager**

```csharp
// tests/Arrr.Tests/Support/FakeSinkManager.cs
using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeSinkManager : ISinkManager
{
    private readonly List<AvailableSinkResponse> _available = [];

    public void Add(AvailableSinkResponse sink) => _available.Add(sink);

    public IReadOnlyList<AvailableSinkResponse> GetAvailable() => _available;

    public Task EnableAsync(string sinkId, CancellationToken ct) => Task.CompletedTask;

    public Task DisableAsync(string sinkId) => Task.CompletedTask;

    public Task ReloadAsync(string sinkId, CancellationToken ct) => Task.CompletedTask;

    public Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default)
        => Task.FromResult<PluginConfigResponse?>(
            new PluginConfigResponse(JsonSerializer.SerializeToElement(new { }), []));

    public Task SaveSinkConfigAsync(string sinkId, JsonElement config, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

- [ ] **Step 2: Write failing tests for SinksEndpoint**

```csharp
// tests/Arrr.Tests/Service/SinksEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;
using Arrr.Core.Services;
using Arrr.Service.Api;
using Arrr.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Arrr.Tests.Service;

[TestFixture]
public class SinksEndpointTests
{
    [Test]
    public async Task GetSinks_WithEmptyApiKeyConfig_Returns503()
    {
        var (client, app, _) = await CreateHostAsync("", []);
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/sinks");
        request.Headers.Add("X-Api-Key", "anything");

        var response = await client.SendAsync(request);

        Assert.That((int)response.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public async Task GetSinks_WithMissingKey_Returns401()
    {
        var (client, app, _) = await CreateHostAsync("secret", []);
        await using var _ = app;

        var response = await client.GetAsync("/api/sinks");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetSinks_WithNoSinks_ReturnsEmptyArray()
    {
        var (client, app, _) = await CreateHostAsync("secret", []);
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/sinks");
        request.Headers.Add("X-Api-Key", "secret");

        var response = await client.SendAsync(request);
        var sinks = await response.Content.ReadFromJsonAsync<AvailableSinkResponse[]>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(sinks, Is.Empty);
    }

    [Test]
    public async Task GetSinks_ReturnsSinkMetadata()
    {
        var sinkA = new AvailableSinkResponse("com.arrr.sink.dbus", "D-Bus", "1.0.0", "Arrr", "Desktop", "🔔", true, true, true, false);
        var (client, app, manager) = await CreateHostAsync("secret", [sinkA]);
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/sinks");
        request.Headers.Add("X-Api-Key", "secret");

        var response = await client.SendAsync(request);
        var sinks = await response.Content.ReadFromJsonAsync<AvailableSinkResponse[]>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(sinks, Has.Length.EqualTo(1));
        Assert.That(sinks![0].Id, Is.EqualTo("com.arrr.sink.dbus"));
    }

    [Test]
    public async Task EnableSink_ReturnsOk()
    {
        var (client, app, _) = await CreateHostAsync("secret", []);
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sinks/com.arrr.sink.dbus/enable");
        request.Headers.Add("X-Api-Key", "secret");

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DisableSink_ReturnsOk()
    {
        var (client, app, _) = await CreateHostAsync("secret", []);
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sinks/com.arrr.sink.dbus/disable");
        request.Headers.Add("X-Api-Key", "secret");

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static async Task<(HttpClient client, WebApplication app, FakeSinkManager manager)> CreateHostAsync(
        string apiKey,
        IEnumerable<AvailableSinkResponse> sinks
    )
    {
        var bus = new EventBusService();
        await bus.StartAsync(CancellationToken.None);

        var manager = new FakeSinkManager();
        foreach (var s in sinks) manager.Add(s);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IConfigService>(new FakeConfigService(apiKey));
        builder.Services.AddSingleton<IEventBus>(bus);
        builder.Services.AddSingleton<ISinkManager>(manager);

        var app = builder.Build();
        app.MapSinksApi();
        await app.StartAsync();

        return (app.GetTestClient(), app, manager);
    }
}
```

- [ ] **Step 3: Run tests — confirm they fail**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "SinksEndpointTests" --logger "console;verbosity=normal"
```

Expected: compile error — `MapSinksApi` does not exist yet.

- [ ] **Step 4: Create SinksEndpoint.cs**

```csharp
// src/Arrr.Service/Api/SinksEndpoint.cs
using System.Text.Json;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class SinksEndpoint
{
    public static IEndpointRouteBuilder MapSinksApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/sinks",
            (HttpContext ctx, IConfigService configService, ISinkManager manager) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                return Results.Ok(manager.GetAvailable());
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/enable",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.EnableAsync(sinkId, ct);
                return Results.Ok(new { sinkId, enabled = true });
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/disable",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.DisableAsync(sinkId);
                return Results.Ok(new { sinkId, enabled = false });
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/reload",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.ReloadAsync(sinkId, ct);
                return Results.Ok(new { sinkId, reloaded = true });
            }
        );

        app.MapGet(
            "/api/sinks/{sinkId}/config",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                var response = await manager.GetSinkConfigAsync(sinkId, ct);
                return response is null
                    ? Results.NotFound(new { sinkId, error = "Sink not found or has no config." })
                    : Results.Ok(response);
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/config",
            async (HttpContext ctx, string sinkId, JsonElement body, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                try
                {
                    await manager.SaveSinkConfigAsync(sinkId, body, ct);
                    return Results.Ok(new { sinkId, saved = true });
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound(new { sinkId, error = "Sink not found." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { sinkId, error = ex.Message });
                }
            }
        );

        return app;
    }
}
```

- [ ] **Step 5: Run tests — confirm they pass**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "SinksEndpointTests" --logger "console;verbosity=normal"
```

Expected: all 6 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Arrr.Service/Api/SinksEndpoint.cs tests/Arrr.Tests/Support/FakeSinkManager.cs tests/Arrr.Tests/Service/SinksEndpointTests.cs
git commit -m "feat(sink): add SinksEndpoint + FakeSinkManager with full test coverage"
```

---

## Task 3: UnixSocketSink (TDD)

Replaces `UnixSocketServer` + `SocketBroadcastSubscriber`.

**Files:**
- Create: `tests/Arrr.Tests/Support/FakeSinkContext.cs`
- Create: `tests/Arrr.Tests/Sinks/UnixSocketSinkTests.cs`
- Create: `src/Arrr.Service/Sinks/UnixSocketSink.cs`
- Delete: `src/Arrr.Service/Internal/UnixSocketServer.cs`
- Delete: `tests/Arrr.Tests/Service/UnixSocketServerTests.cs`

- [ ] **Step 1: Create FakeSinkContext**

```csharp
// tests/Arrr.Tests/Support/FakeSinkContext.cs
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Support;

internal class FakeSinkContext : ISinkContext
{
    public ILogger Logger { get; } = NullLogger.Instance;
    public string ConfigPath { get; }

    public FakeSinkContext(string configPath = "")
    {
        ConfigPath = configPath;
    }

    public Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
        => Task.FromResult(new T());

    public Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

- [ ] **Step 2: Write failing tests for UnixSocketSink**

```csharp
// tests/Arrr.Tests/Sinks/UnixSocketSinkTests.cs
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Service.Sinks;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sinks;

[TestFixture]
public class UnixSocketSinkTests
{
    private string _socketPath = "";

    [SetUp]
    public void SetUp()
        => _socketPath = Path.Combine(Path.GetTempPath(), $"arrr_sink_test_{Guid.NewGuid()}.sock");

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_socketPath)) File.Delete(_socketPath);
    }

    [Test]
    public async Task StartAsync_CreatesSocketFile()
    {
        var sink = new UnixSocketSink();
        var ctx = new FakeSinkContext(socketPath: _socketPath);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sink.StartAsync(ctx, cts.Token);

        Assert.That(File.Exists(_socketPath), Is.True);

        await sink.StopAsync();
    }

    [Test]
    public async Task ConsumeAsync_BroadcastsJsonLine_ToConnectedClient()
    {
        var sink = new UnixSocketSink();
        var ctx = new FakeSinkContext(socketPath: _socketPath);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sink.StartAsync(ctx, cts.Token);

        await WaitForSocketAsync(_socketPath, cts.Token);

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), cts.Token);
        using var stream = new NetworkStream(client, false);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await sink.ConsumeAsync(notification, cts.Token);

        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, cts.Token);
        var line = Encoding.UTF8.GetString(buffer, 0, read).TrimEnd('\n');
        var received = JsonSerializer.Deserialize<Notification>(line);

        Assert.That(received, Is.EqualTo(notification));

        await sink.StopAsync();
    }

    [Test]
    public async Task StopAsync_RemovesSocketFile()
    {
        var sink = new UnixSocketSink();
        var ctx = new FakeSinkContext(socketPath: _socketPath);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sink.StartAsync(ctx, cts.Token);
        await sink.StopAsync();

        Assert.That(File.Exists(_socketPath), Is.False);
    }

    [Test]
    public async Task StopAsync_WhenNeverStarted_DoesNotThrow()
    {
        var sink = new UnixSocketSink();
        Assert.DoesNotThrowAsync(() => sink.StopAsync());
    }

    private static async Task WaitForSocketAsync(string path, CancellationToken ct)
    {
        while (!File.Exists(path))
            await Task.Delay(20, ct);
    }
}
```

Note: `FakeSinkContext` needs a `socketPath` parameter to override config. Update the fake:

```csharp
// tests/Arrr.Tests/Support/FakeSinkContext.cs  (updated)
using Arrr.Core.Interfaces;
using Arrr.Service.Sinks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Support;

internal class FakeSinkContext : ISinkContext
{
    private readonly string? _socketPath;

    public ILogger Logger { get; } = NullLogger.Instance;
    public string ConfigPath { get; }

    public FakeSinkContext(string configPath = "", string? socketPath = null)
    {
        ConfigPath = configPath;
        _socketPath = socketPath;
    }

    public Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        if (_socketPath is not null && typeof(T) == typeof(UnixSocketSinkConfig))
        {
            var cfg = new UnixSocketSinkConfig { SocketPath = _socketPath };
            return Task.FromResult((T)(object)cfg);
        }
        return Task.FromResult(new T());
    }

    public Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

- [ ] **Step 3: Run tests — confirm they fail**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "UnixSocketSinkTests" --logger "console;verbosity=normal"
```

Expected: compile error — `UnixSocketSink` and `UnixSocketSinkConfig` do not exist yet.

- [ ] **Step 4: Create UnixSocketSink.cs**

```csharp
// src/Arrr.Service/Sinks/UnixSocketSink.cs
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Arrr.Service.Sinks;

public class UnixSocketSinkConfig
{
    [Description("Unix domain socket path for streaming notifications as newline-delimited JSON")]
    public string SocketPath { get; set; } = "/tmp/arrr.sock";
}

internal class UnixSocketSink : ISinkPlugin, IConfigurablePlugin
{
    private readonly List<NetworkStream> _clients = [];
    private readonly SemaphoreSlim _clientsLock = new(1, 1);

    private Socket? _listener;
    private ISinkContext? _context;
    private string _socketPath = "/tmp/arrr.sock";
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptTask;

    public string Id          => "com.arrr.sink.socket";
    public string Name        => "Unix Socket";
    public string Version     => "1.0.0";
    public string Author      => "Arrr";
    public string Description => "Broadcasts notifications as newline-delimited JSON on a Unix domain socket.";
    public string Icon        => "🔌";
    public Type   ConfigType  => typeof(UnixSocketSinkConfig);

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        var config = await context.LoadConfigAsync<UnixSocketSinkConfig>(ct);
        _socketPath = config.SocketPath;

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(10);

        context.Logger.LogInformation("Unix socket sink listening on {Path}", _socketPath);

        _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _acceptTask = AcceptClientsAsync(_acceptCts.Token);
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification) + "\n");

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

    public async Task StopAsync()
    {
        if (_acceptCts is not null)
        {
            await _acceptCts.CancelAsync();
            if (_acceptTask is not null)
                await _acceptTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await _clientsLock.WaitAsync();
        foreach (var client in _clients)
            client.Dispose();
        _clients.Clear();
        _clientsLock.Release();

        _listener?.Dispose();
        _listener = null;

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
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

                _context?.Logger.LogDebug("Client connected, total: {Count}", _clients.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _context?.Logger.LogError(ex, "Error accepting client"); }
        }
    }
}
```

- [ ] **Step 5: Delete old UnixSocketServer and its tests**

```bash
rm src/Arrr.Service/Internal/UnixSocketServer.cs
rm tests/Arrr.Tests/Service/UnixSocketServerTests.cs
```

- [ ] **Step 6: Run tests — confirm they pass**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "UnixSocketSinkTests" --logger "console;verbosity=normal"
```

Expected: 4 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Arrr.Service/Sinks/UnixSocketSink.cs tests/Arrr.Tests/Support/FakeSinkContext.cs tests/Arrr.Tests/Sinks/UnixSocketSinkTests.cs
git commit -m "feat(sink): add UnixSocketSink replacing UnixSocketServer"
```

---

## Task 4: DbusNotifySink (TDD)

**Files:**
- Create: `tests/Arrr.Tests/Sinks/DbusNotifySinkTests.cs`
- Create: `src/Arrr.Service/Sinks/DbusNotifySink.cs`
- Delete: `src/Arrr.Service/Subscribers/DBusNotifySubscriber.cs`
- Delete: `src/Arrr.Service/Subscribers/SocketBroadcastSubscriber.cs`
- Delete: `tests/Arrr.Tests/Service/DBusNotifySubscriberTests.cs`

- [ ] **Step 1: Write failing tests for DbusNotifySink**

```csharp
// tests/Arrr.Tests/Sinks/DbusNotifySinkTests.cs
using Arrr.Core.Data.Notifications;
using Arrr.Service.Sinks;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sinks;

[TestFixture]
public class DbusNotifySinkTests
{
    [Test]
    public async Task StartAsync_WhenSessionBusUnavailable_DoesNotThrow()
    {
        var sink = new DbusNotifySink();
        var ctx = new FakeSinkContext();

        using var cts = new CancellationTokenSource();
        Assert.DoesNotThrowAsync(() => sink.StartAsync(ctx, cts.Token));

        await sink.StopAsync();
    }

    [Test]
    public async Task StopAsync_WhenNeverStarted_DoesNotThrow()
    {
        var sink = new DbusNotifySink();
        Assert.DoesNotThrowAsync(() => sink.StopAsync());
    }

    [Test]
    public async Task ConsumeAsync_WhenNotConnected_DoesNotThrow()
    {
        var sink = new DbusNotifySink();
        var ctx = new FakeSinkContext();

        using var cts = new CancellationTokenSource();
        await sink.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Hello", "World", DateTimeOffset.UtcNow, null);

        Assert.DoesNotThrowAsync(() => sink.ConsumeAsync(notification, cts.Token));

        await sink.StopAsync();
    }
}
```

- [ ] **Step 2: Run tests — confirm they fail**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "DbusNotifySinkTests" --logger "console;verbosity=normal"
```

Expected: compile error — `DbusNotifySink` does not exist yet.

- [ ] **Step 3: Create DbusNotifySink.cs**

```csharp
// src/Arrr.Service/Sinks/DbusNotifySink.cs
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.DBus;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace Arrr.Service.Sinks;

internal class DbusNotifySink : ISinkPlugin
{
    private Connection? _connection;
    private INotifications? _proxy;
    private ISinkContext? _context;

    public string Id          => "com.arrr.sink.dbus";
    public string Name        => "D-Bus Notifications";
    public string Version     => "1.0.0";
    public string Author      => "Arrr";
    public string Description => "Delivers notifications as native desktop popups via org.freedesktop.Notifications.";
    public string Icon        => "🔔";

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;

        try
        {
            _connection = new Connection(Address.Session);
            await _connection.ConnectAsync();
            _proxy = _connection.CreateProxy<INotifications>(
                "org.freedesktop.Notifications",
                "/org/freedesktop/Notifications"
            );
            context.Logger.LogInformation("D-Bus sink connected");
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "D-Bus session bus unavailable — desktop notifications disabled");
            _connection = null;
            _proxy = null;
        }
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_proxy is null)
            return;

        try
        {
            await _proxy.NotifyAsync(
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
            _context?.Logger.LogError(ex, "Failed to send D-Bus notification: {Title}", notification.Title);
        }
    }

    public Task StopAsync()
    {
        _connection?.Dispose();
        _connection = null;
        _proxy = null;

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Delete old subscriber files and their tests**

```bash
rm src/Arrr.Service/Subscribers/DBusNotifySubscriber.cs
rm src/Arrr.Service/Subscribers/SocketBroadcastSubscriber.cs
rm tests/Arrr.Tests/Service/DBusNotifySubscriberTests.cs
```

- [ ] **Step 5: Run tests — confirm they pass**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "DbusNotifySinkTests" --logger "console;verbosity=normal"
```

Expected: 3 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Arrr.Service/Sinks/DbusNotifySink.cs tests/Arrr.Tests/Sinks/DbusNotifySinkTests.cs
git commit -m "feat(sink): add DbusNotifySink replacing DBusNotifySubscriber"
```

---

## Task 5: SinkOrchestrator (TDD)

**Files:**
- Create: `tests/Arrr.Tests/Support/FakeSink.cs`
- Create: `tests/Arrr.Tests/Service/SinkOrchestratorTests.cs`
- Create: `src/Arrr.Service/Internal/SinkOrchestrator.cs`

- [ ] **Step 1: Create FakeSink**

```csharp
// tests/Arrr.Tests/Support/FakeSink.cs
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeSink : ISinkPlugin
{
    public List<Notification> Received { get; } = [];
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }

    public string Id          { get; }
    public string Name        => "Fake Sink";
    public string Version     => "1.0.0";
    public string Author      => "Test";
    public string Description => "Test sink";
    public string Icon        => "";

    public FakeSink(string id = "com.test.sink")
    {
        Id = id;
    }

    public Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        Started = true;
        return Task.CompletedTask;
    }

    public Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        Received.Add(notification);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stopped = true;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write failing tests for SinkOrchestrator**

```csharp
// tests/Arrr.Tests/Service/SinkOrchestratorTests.cs
using Arrr.Core.Data.Config;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;
using Arrr.Service.Internal;
using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class SinkOrchestratorTests
{
    [Test]
    public async Task FanOut_DeliversNotification_ToAllRunningSinks()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await bus.StartAsync(cts.Token);

        var sinkA = new FakeSink("com.test.sink.a");
        var sinkB = new FakeSink("com.test.sink.b");

        var orchestrator = new SinkOrchestrator(bus);
        orchestrator.AddSinkForTest(sinkA, enabled: true);
        orchestrator.AddSinkForTest(sinkB, enabled: true);
        await orchestrator.StartSinksForTestAsync(cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Hi", "Body", DateTimeOffset.UtcNow, null);
        await bus.PublishAsync(notification, cts.Token);

        await Task.Delay(100, cts.Token);

        Assert.That(sinkA.Received, Has.Count.EqualTo(1));
        Assert.That(sinkB.Received, Has.Count.EqualTo(1));

        await bus.StopAsync(cts.Token);
    }

    [Test]
    public async Task FanOut_WhenOneSinkThrows_OtherSinkStillReceives()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await bus.StartAsync(cts.Token);

        var throwing = new ThrowingSink("com.test.throwing");
        var good = new FakeSink("com.test.good");

        var orchestrator = new SinkOrchestrator(bus);
        orchestrator.AddSinkForTest(throwing, enabled: true);
        orchestrator.AddSinkForTest(good, enabled: true);
        await orchestrator.StartSinksForTestAsync(cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Hi", "Body", DateTimeOffset.UtcNow, null);
        await bus.PublishAsync(notification, cts.Token);

        await Task.Delay(100, cts.Token);

        Assert.That(good.Received, Has.Count.EqualTo(1));

        await bus.StopAsync(cts.Token);
    }
}

internal class ThrowingSink : ISinkPlugin
{
    public string Id => _id;
    public string Name => "Throwing"; public string Version => "1.0.0";
    public string Author => "Test"; public string Description => ""; public string Icon => "";
    private readonly string _id;
    public ThrowingSink(string id) { _id = id; }
    public Task StartAsync(ISinkContext context, CancellationToken ct) => Task.CompletedTask;
    public Task ConsumeAsync(Notification notification, CancellationToken ct) => throw new Exception("boom");
    public Task StopAsync() => Task.CompletedTask;
}
```

- [ ] **Step 3: Run tests — confirm they fail**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "SinkOrchestratorTests" --logger "console;verbosity=normal"
```

Expected: compile error — `SinkOrchestrator` does not exist yet.

- [ ] **Step 4: Create SinkOrchestrator.cs**

```csharp
// src/Arrr.Service/Internal/SinkOrchestrator.cs
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Arrr.Core.Attributes;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Config;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Arrr.Service.Sinks;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Arrr.Service.Internal;

internal class SinkOrchestrator : BackgroundService, ISinkManager
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly Serilog.ILogger _logger = Log.ForContext<SinkOrchestrator>();
    private readonly IEventBus _eventBus;
    private readonly IConfigService? _configService;
    private readonly DirectoriesConfig? _directoriesConfig;
    private readonly string? _pluginsPath;

    private readonly List<(ISinkPlugin Sink, bool IsBuiltIn)> _known = [];
    private readonly HashSet<string> _running = [];

    public SinkOrchestrator(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public SinkOrchestrator(IEventBus eventBus, IConfigService configService, DirectoriesConfig directoriesConfig)
    {
        _eventBus = eventBus;
        _configService = configService;
        _directoriesConfig = directoriesConfig;
        _pluginsPath = directoriesConfig[DirectoryType.Plugins];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _known.Add((new DbusNotifySink(), true));
        _known.Add((new UnixSocketSink(), true));

        if (_pluginsPath is not null)
            LoadExternalSinks();

        _eventBus.Subscribe<Notification>(FanOutAsync);

        var config = _configService?.Config;

        foreach (var (sink, isBuiltIn) in _known)
        {
            var entry = config?.Sinks.FirstOrDefault(s => s.Id == sink.Id);
            var shouldStart = entry is null ? isBuiltIn : entry.Enabled;

            if (shouldStart)
                await StartSinkAsync(sink, stoppingToken);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        foreach (var id in _running.ToList())
        {
            var sink = _known.FirstOrDefault(k => k.Sink.Id == id).Sink;
            if (sink is not null)
            {
                try { await sink.StopAsync(); }
                catch (Exception ex) { _logger.Error(ex, "Error stopping sink {Id}", id); }
            }
        }
        _running.Clear();
    }

    private async Task StartSinkAsync(ISinkPlugin sink, CancellationToken ct)
    {
        try
        {
            var context = CreateContext(sink);
            await sink.StartAsync(context, ct);
            _running.Add(sink.Id);
            _logger.Information("Started sink: {Id}", sink.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start sink {Id}", sink.Id);
        }
    }

    private async Task FanOutAsync(Notification notification, CancellationToken ct)
    {
        foreach (var (sink, _) in _known)
        {
            if (!_running.Contains(sink.Id))
                continue;

            try { await sink.ConsumeAsync(notification, ct); }
            catch (Exception ex) { _logger.Error(ex, "Sink {Id} consume error", sink.Id); }
        }
    }

    private void LoadExternalSinks()
    {
        foreach (var dll in Directory.EnumerateFiles(_pluginsPath!, "*.dll"))
        {
            try
            {
                var ctx = new PluginLoadContext(dll);
                var asm = ctx.LoadFromAssemblyPath(dll);
                var sinkType = asm.GetTypes()
                                  .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISinkPlugin)));

                if (sinkType is null) { ctx.Unload(); continue; }
                if (Activator.CreateInstance(sinkType) is not ISinkPlugin sink) { ctx.Unload(); continue; }

                _known.Add((sink, false));
                _logger.Information("Discovered external sink: {Id}", sink.Id);
            }
            catch (Exception ex) { _logger.Error(ex, "Failed to inspect {Path} for sink types", dll); }
        }
    }

    private ISinkContext CreateContext(ISinkPlugin sink)
    {
        var configPath = _directoriesConfig is not null
            ? Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{sink.Id}.config")
            : Path.Combine(Path.GetTempPath(), $"{sink.Id}.config");

        var logPath = _directoriesConfig is not null
            ? Path.Combine(_directoriesConfig[DirectoryType.Logs], "sinks", $"{sink.Id}.log")
            : Path.Combine(Path.GetTempPath(), $"{sink.Id}.log");

        var serilogLogger = new LoggerConfiguration()
                            .MinimumLevel.Debug()
                            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                            .CreateLogger();

        var msLogger = new SerilogLoggerFactory(serilogLogger).CreateLogger(sink.Id);

        return new SinkContext(configPath, msLogger);
    }

    public IReadOnlyList<AvailableSinkResponse> GetAvailable()
    {
        return _known.Select(k =>
        {
            var entry = _configService?.Config.Sinks.FirstOrDefault(s => s.Id == k.Sink.Id);
            var enabled = entry is null ? k.IsBuiltIn : entry.Enabled;

            return new AvailableSinkResponse(
                k.Sink.Id, k.Sink.Name, k.Sink.Version, k.Sink.Author, k.Sink.Description, k.Sink.Icon,
                Enabled: enabled,
                Running: _running.Contains(k.Sink.Id),
                IsBuiltIn: k.IsBuiltIn,
                HasConfig: k.Sink is IConfigurablePlugin
            );
        }).ToList();
    }

    public async Task EnableAsync(string sinkId, CancellationToken ct)
    {
        if (_configService is not null)
        {
            var entry = _configService.Config.Sinks.FirstOrDefault(s => s.Id == sinkId);
            if (entry is null)
                _configService.Config.Sinks.Add(new SinkEntry { Id = sinkId, Enabled = true });
            else
                entry.Enabled = true;
            _configService.Save();
        }

        var sink = _known.FirstOrDefault(k => k.Sink.Id == sinkId).Sink;
        if (sink is not null && !_running.Contains(sinkId))
            await StartSinkAsync(sink, ct);
    }

    public async Task DisableAsync(string sinkId)
    {
        if (_configService is not null)
        {
            var entry = _configService.Config.Sinks.FirstOrDefault(s => s.Id == sinkId);
            if (entry is not null) entry.Enabled = false;
            _configService.Save();
        }

        if (!_running.Contains(sinkId))
            return;

        var sink = _known.FirstOrDefault(k => k.Sink.Id == sinkId).Sink;
        if (sink is not null)
        {
            try { await sink.StopAsync(); }
            catch (Exception ex) { _logger.Error(ex, "Error stopping sink {Id}", sinkId); }
        }

        _running.Remove(sinkId);
        _logger.Information("Disabled sink: {Id}", sinkId);
    }

    public async Task ReloadAsync(string sinkId, CancellationToken ct)
    {
        var sink = _known.FirstOrDefault(k => k.Sink.Id == sinkId).Sink;
        if (sink is null) { _logger.Warning("Sink {Id} not found for reload", sinkId); return; }

        if (_running.Contains(sinkId))
        {
            try { await sink.StopAsync(); }
            catch (Exception ex) { _logger.Error(ex, "Error stopping sink {Id} during reload", sinkId); }
            _running.Remove(sinkId);
        }

        await StartSinkAsync(sink, ct);
    }

    public async Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default)
    {
        var sink = _known.FirstOrDefault(k => k.Sink.Id == sinkId).Sink;
        if (sink is not IConfigurablePlugin configurable)
            return null;

        var configPath = _directoriesConfig is not null
            ? Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{sinkId}.config")
            : Path.Combine(Path.GetTempPath(), $"{sinkId}.config");

        var configType = configurable.ConfigType;
        object config;

        if (File.Exists(configPath))
        {
            await using var stream = File.OpenRead(configPath);
            config = (await JsonSerializer.DeserializeAsync(stream, configType, _jsonOpts, ct))
                     ?? Activator.CreateInstance(configType)!;
        }
        else
        {
            config = Activator.CreateInstance(configType)!;
        }

        EncryptionUtils.ApplySensitiveFields(config, decrypt: true);
        var values = JsonSerializer.SerializeToElement(config, configType, _jsonOpts);
        var schema = BuildSchema(configType);
        return new PluginConfigResponse(values, schema);
    }

    public async Task SaveSinkConfigAsync(string sinkId, JsonElement incoming, CancellationToken ct = default)
    {
        var sink = _known.FirstOrDefault(k => k.Sink.Id == sinkId).Sink
                   ?? throw new KeyNotFoundException($"Sink '{sinkId}' not found.");

        if (sink is not IConfigurablePlugin configurable)
            throw new InvalidOperationException($"Sink '{sinkId}' does not expose a config type.");

        var configType = configurable.ConfigType;
        var config = incoming.Deserialize(configType, _jsonOpts) ?? Activator.CreateInstance(configType)!;
        EncryptionUtils.ApplySensitiveFields(config, decrypt: false);

        var configPath = _directoriesConfig is not null
            ? Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{sinkId}.config")
            : Path.Combine(Path.GetTempPath(), $"{sinkId}.config");

        var dir = Path.GetDirectoryName(configPath);
        if (dir is not null) Directory.CreateDirectory(dir);

        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(stream, config, configType, _jsonOpts, ct);
    }

    private static ConfigFieldInfo[] BuildSchema(Type configType) =>
        configType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Select(p => new ConfigFieldInfo(
                      p.Name,
                      p.GetCustomAttribute<DescriptionAttribute>()?.Description,
                      p.GetCustomAttribute<SensitiveAttribute>() is not null
                  ))
                  .ToArray();

    // Test-only helpers (internal visibility via InternalsVisibleTo)
    internal void AddSinkForTest(ISinkPlugin sink, bool enabled)
    {
        _known.Add((sink, false));
        if (enabled) _running.Add(sink.Id);
    }

    internal async Task StartSinksForTestAsync(CancellationToken ct)
    {
        _eventBus.Subscribe<Notification>(FanOutAsync);
        foreach (var (sink, _) in _known.Where(k => _running.Contains(k.Sink.Id)))
            await sink.StartAsync(new FakeContextForTest(), ct);
    }

    private sealed class FakeContextForTest : ISinkContext
    {
        public ILogger Logger { get; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public string ConfigPath => "";
        public Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new() => Task.FromResult(new T());
        public Task SaveConfigAsync<T>(T config, CancellationToken ct = default) => Task.CompletedTask;
    }
}

internal sealed class SinkContext : ISinkContext
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ConfigPath { get; }
    public ILogger Logger    { get; }

    public SinkContext(string configPath, ILogger logger)
    {
        ConfigPath = configPath;
        Logger = logger;
    }

    public async Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        if (!File.Exists(ConfigPath))
            return new T();

        await using var stream = File.OpenRead(ConfigPath);
        var config = await JsonSerializer.DeserializeAsync<T>(stream, _opts, ct) ?? new T();
        EncryptionUtils.ApplySensitiveFields(config, decrypt: true);
        return config;
    }

    public async Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
    {
        EncryptionUtils.ApplySensitiveFields(config!, decrypt: false);
        var dir = Path.GetDirectoryName(ConfigPath);
        if (dir is not null) Directory.CreateDirectory(dir);
        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, _opts, ct);
    }
}
```

- [ ] **Step 5: Run tests — confirm they pass**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "SinkOrchestratorTests" --logger "console;verbosity=normal"
```

Expected: 2 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Arrr.Service/Internal/SinkOrchestrator.cs tests/Arrr.Tests/Support/FakeSink.cs tests/Arrr.Tests/Service/SinkOrchestratorTests.cs
git commit -m "feat(sink): add SinkOrchestrator with fan-out and lifecycle management"
```

---

## Task 6: Wire up Program.cs + cleanup

**Files:**
- Modify: `src/Arrr.Service/Program.cs`

- [ ] **Step 1: Update Program.cs**

Replace the relevant DI registrations. Remove:
```csharp
builder.Services.AddSingleton<UnixSocketServer>(...);
builder.Services.AddSingleton<SocketBroadcastSubscriber>();
builder.Services.AddHostedService<DBusNotifySubscriber>();
```
And the `app.Services.GetRequiredService<SocketBroadcastSubscriber>();` line.

Add after the `EventBusHostedService` registration:
```csharp
builder.Services.AddSingleton<SinkOrchestrator>(sp => new SinkOrchestrator(
    sp.GetRequiredService<IEventBus>(),
    sp.GetRequiredService<IConfigService>(),
    sp.GetRequiredService<DirectoriesConfig>()
));
builder.Services.AddHostedService(sp => sp.GetRequiredService<SinkOrchestrator>());
builder.Services.AddSingleton<ISinkManager>(sp => sp.GetRequiredService<SinkOrchestrator>());
```

Also add `app.MapSinksApi();` after `app.MapPluginCallbacks(ct);`.

Remove the `using Arrr.Service.Subscribers;` import.
Add `using Arrr.Service.Sinks;` (for `UnixSocketSinkConfig` reference if needed — actually not needed since SinkOrchestrator handles it internally).

Remove the `SocketPath` reference: `new(sp.GetRequiredService<IConfigService>().Config.SocketPath)` is deleted with `UnixSocketServer`.

The full updated `Program.cs` DI block:

```csharp
builder.Services.AddSingleton(directoriesConfig);
builder.Services.AddSingleton<IConfigService, ConfigService>();
builder.Services.AddSingleton<EventBusService>();
builder.Services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBusService>());
builder.Services.AddSingleton<IPluginRegistry, PluginRegistryService>();
builder.Services.AddSingleton<PluginContextFactory>();
builder.Services.AddSingleton<NuGetPluginInstaller>();
builder.Services.AddSingleton<IPluginInstaller>(sp => sp.GetRequiredService<NuGetPluginInstaller>());
builder.Services.AddSingleton<PluginOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PluginOrchestrator>());
builder.Services.AddSingleton<IPluginManager>(sp => sp.GetRequiredService<PluginOrchestrator>());
builder.Services.AddHostedService<EventBusHostedService>();
builder.Services.AddSingleton<SinkOrchestrator>(sp => new SinkOrchestrator(
    sp.GetRequiredService<IEventBus>(),
    sp.GetRequiredService<IConfigService>(),
    sp.GetRequiredService<DirectoriesConfig>()
));
builder.Services.AddHostedService(sp => sp.GetRequiredService<SinkOrchestrator>());
builder.Services.AddSingleton<ISinkManager>(sp => sp.GetRequiredService<SinkOrchestrator>());
builder.Services.AddOpenApi();
builder.Logging.ClearProviders().AddSerilog();
```

- [ ] **Step 2: Build the full solution — zero errors**

```bash
dotnet build src/Arrr.Service/Arrr.Service.csproj -c Release
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Run all tests**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "Category!=Integration" --logger "console;verbosity=normal"
```

Expected: all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/Arrr.Service/Program.cs
git commit -m "feat(sink): wire SinkOrchestrator into DI, remove old subscriber registrations"
```

---

## Task 7: UI — Output Connectors section

**Files:**
- Modify: `ui/src/types.ts`
- Modify: `ui/src/api.ts`
- Modify: `ui/src/App.tsx`

- [ ] **Step 1: Add Sink type to types.ts**

Add after the `Plugin` interface:

```typescript
// ui/src/types.ts  (additions)
export interface Sink {
  id: string
  name: string
  version: string
  author: string
  description: string
  icon: string
  enabled: boolean
  running: boolean
  isBuiltIn: boolean
  hasConfig: boolean
}
```

- [ ] **Step 2: Add sink methods to ArrrApi in api.ts**

Add these methods inside the `ArrrApi` class in `ui/src/api.ts`, after `getQrCode`:

```typescript
  async getSinks(): Promise<Sink[]> {
    return (await this.req('/api/sinks')).json()
  }

  async enableSink(id: string) {
    await this.req(`/api/sinks/${encodeURIComponent(id)}/enable`, { method: 'POST' })
  }

  async disableSink(id: string) {
    await this.req(`/api/sinks/${encodeURIComponent(id)}/disable`, { method: 'POST' })
  }

  async reloadSink(id: string) {
    await this.req(`/api/sinks/${encodeURIComponent(id)}/reload`, { method: 'POST' })
  }

  async getSinkConfig(sinkId: string): Promise<PluginConfigResponse> {
    return (await this.req(`/api/sinks/${encodeURIComponent(sinkId)}/config`)).json()
  }

  async saveSinkConfig(sinkId: string, config: Record<string, unknown>) {
    await this.req(`/api/sinks/${encodeURIComponent(sinkId)}/config`, {
      method: 'POST',
      body: JSON.stringify(config),
    })
  }
```

Also add `Sink` to the import at the top of `api.ts`:

```typescript
import type { Plugin, PluginConfigResponse, Sink } from './types'
```

- [ ] **Step 3: Add Output Connectors section to App.tsx**

Add the following state declarations near the existing `plugins` state:

```typescript
const [sinks, setSinks] = useState<Sink[]>([])
const [sinkBusy, setSinkBusy] = useState<Record<string, boolean>>({})
const [configuringSink, setConfiguringSink] = useState<Sink | null>(null)
```

Add a `loadSinks` function alongside `loadPlugins`. `api` is the existing `ArrrApi` instance already used in `App.tsx`:

```typescript
const loadSinks = useCallback(async () => {
  try {
    const data = await api.getSinks()
    setSinks(data)
  } catch { /* ignore */ }
}, [api])
```

Call `loadSinks()` in the same `useEffect` that calls `loadPlugins()`.

Add sink toggle/reload/configure handlers:

```typescript
const handleSinkToggle = async (sink: Sink, enabled: boolean) => {
  setSinkBusy(b => ({ ...b, [sink.id]: true }))
  try {
    if (enabled) await api.enableSink(sink.id)
    else await api.disableSink(sink.id)
    await loadSinks()
  } finally {
    setSinkBusy(b => ({ ...b, [sink.id]: false }))
  }
}

const handleSinkReload = async (sink: Sink) => {
  setSinkBusy(b => ({ ...b, [sink.id]: true }))
  try {
    await api.reloadSink(sink.id)
    await loadSinks()
  } finally {
    setSinkBusy(b => ({ ...b, [sink.id]: false }))
  }
}
```

`ConfigModal` accepts `api: ArrrApi` and calls `api.getConfig(id)` / `api.saveConfig(id, cfg)`. For sinks, create an inline adapter object that delegates to the sink-specific methods:

```typescript
const sinkApiAdapter = (sink: Sink) => ({
  ...api,
  getConfig: (id: string) => api.getSinkConfig(id),
  saveConfig: (id: string, cfg: Record<string, unknown>) => api.saveSinkConfig(id, cfg),
}) as unknown as ArrrApi
```

Add the Output Connectors section in JSX, after the existing plugins grid and before the closing container:

```tsx
{/* Output Connectors */}
<Box mt={10}>
  <Text
    fontFamily="'Pirata One', cursive"
    fontSize="xl"
    color="amber.400"
    letterSpacing="wider"
    mb={4}
  >
    Output Connectors
  </Text>

  {sinks.length === 0 ? (
    <Text color="gray.600" fontFamily="mono" fontSize="sm">No output connectors found.</Text>
  ) : (
    <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} gap={4}>
      {sinks.map(sink => (
        <PluginCard
          key={sink.id}
          plugin={{
            ...sink,
            categories: [],
            hasCallback: false,
            hasQr: false,
          }}
          busy={sinkBusy[sink.id] ?? false}
          onToggle={(_, enabled) => handleSinkToggle(sink, enabled)}
          onReload={() => handleSinkReload(sink)}
          onUninstall={() => {}}
          onConfigure={() => setConfiguringSink(sink)}
          onCallback={() => {}}
          onQr={() => {}}
        />
      ))}
    </SimpleGrid>
  )}
</Box>

{configuringSink && (
  <ConfigModal
    plugin={{
      ...configuringSink,
      categories: [],
      hasCallback: false,
      hasQr: false,
    }}
    api={sinkApiAdapter(configuringSink)}
    onClose={() => setConfiguringSink(null)}
    onToast={showToast}
  />
)}

- [ ] **Step 4: Build UI — no TypeScript errors**

```bash
cd ui && npm run build
```

Expected: no TypeScript errors, no chunk size warnings above limit.

- [ ] **Step 5: Commit**

```bash
git add ui/src/types.ts ui/src/api.ts ui/src/App.tsx
git commit -m "feat(sink): add Output Connectors section to UI"
```

---

## Task 8: Final verification

- [ ] **Step 1: Run all tests**

```bash
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release --filter "Category!=Integration" --logger "console;verbosity=normal"
```

Expected: all tests PASS, zero failures.

- [ ] **Step 2: Build service**

```bash
dotnet build src/Arrr.Service/Arrr.Service.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Build UI**

```bash
cd ui && npm run build
```

Expected: no errors.

- [ ] **Step 4: Push and open PR**

```bash
git push origin develop
gh pr create --base main --head develop --label release --title "feat(sink): pluggable output connectors (ISinkPlugin system)" --body "..."
```
