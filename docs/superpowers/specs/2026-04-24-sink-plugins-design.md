# Sink Plugin System — Design Spec

**Goal:** Make the output side of Arrr pluggable via an `ISinkPlugin` interface, symmetric to `ISourcePlugin`. D-Bus and Unix socket become built-in activatable sinks; external sinks can be loaded from `plugins/` assemblies.

**Architecture:** `SinkOrchestrator` manages sink lifecycle and fans out `Notification` events from the `EventBus` to all active sinks. Built-in sinks (D-Bus, socket) are always present but can be stopped at runtime. External sinks load from the same `plugins/` directory as source plugins.

**Tech Stack:** .NET 10, Tmds.DBus, existing `IConfigurablePlugin` / `AssemblyLoadContext` patterns.

---

## Interfaces (Arrr.Core)

### `Interfaces/ISinkPlugin.cs`

```csharp
namespace Arrr.Core.Interfaces;

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

### `Interfaces/ISinkContext.cs`

```csharp
namespace Arrr.Core.Interfaces;

public interface ISinkContext
{
    ILogger Logger    { get; }
    string ConfigPath { get; }
}
```

Sinks that need user-editable configuration implement `IConfigurablePlugin` (already in `Arrr.Core`) — exact same pattern as source plugins.

---

## Built-in Sinks (Arrr.Service)

Located under `src/Arrr.Service/Sinks/`.

### `DbusNotifySink`

- **ID:** `com.arrr.sink.dbus`
- **Configurable:** no
- Moves all D-Bus logic out of `DBusNotifySubscriber` (which is deleted)
- `StartAsync`: opens D-Bus session connection
- `ConsumeAsync`: calls `INotifications.NotifyAsync`
- `StopAsync`: closes D-Bus connection

### `UnixSocketSink`

- **ID:** `com.arrr.sink.socket`
- **Configurable:** yes — `socketPath` (default `/tmp/arrr.sock`)
- Absorbs `UnixSocketServer` entirely (class becomes private/internal to this sink)
- `StartAsync`: binds and listens on the configured socket path
- `ConsumeAsync`: broadcasts JSON newline to all connected clients
- `StopAsync`: closes all client connections, deletes socket file

**Deleted after migration:**
- `src/Arrr.Service/Subscribers/DBusNotifySubscriber.cs`
- `src/Arrr.Service/Subscribers/SocketBroadcastSubscriber.cs`
- `src/Arrr.Service/Internal/UnixSocketServer.cs` (absorbed into `UnixSocketSink`)

---

## SinkOrchestrator (Arrr.Service)

`src/Arrr.Service/Internal/SinkOrchestrator.cs`

Responsibilities:
- Holds the list of all known sinks (built-in + loaded from `plugins/`)
- At startup: instantiates `DbusNotifySink` and `UnixSocketSink`, starts the enabled ones
- Scans `plugins/*.dll` for types implementing `ISinkPlugin`, loads them via `PluginLoadContext`
- Subscribes **once** to `IEventBus` for `Notification` events; fans out to all active (running) sinks sequentially — a failing sink logs the error and does not stop fan-out
- Exposes `GetAvailable()`, `EnableAsync()`, `DisableAsync()`, `ReloadAsync()`, `GetPendingQrCode()` (sinks can also implement `IQrPlugin`)
- Config/save via `IConfigurablePlugin` — same flow as source plugins

### `ISinkManager.cs` (Arrr.Core)

```csharp
namespace Arrr.Core.Interfaces;

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

---

## Data (Arrr.Core)

### `Data/Api/AvailableSinkResponse.cs`

```csharp
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

---

## Configuration

`arrr.config` gains a `sinks` array (parallel to `plugins`):

```json
{
  "sinks": [
    { "id": "com.arrr.sink.dbus",   "enabled": true },
    { "id": "com.arrr.sink.socket", "enabled": true }
  ]
}
```

`socketPath` moves from the root config into the `UnixSocketSink` per-sink config. A migration default keeps `/tmp/arrr.sock` if not set.

---

## REST API

New endpoints under `/api/sinks`, symmetric to `/api/plugins`:

```
GET  /api/sinks                       → AvailableSinkResponse[]
POST /api/sinks/{id}/enable
POST /api/sinks/{id}/disable
POST /api/sinks/{id}/reload
GET  /api/sinks/{id}/config           → PluginConfigResponse
POST /api/sinks/{id}/config           → saves config
```

Implemented in `src/Arrr.Service/Api/SinksEndpoint.cs`.

---

## UI

New **"Output Connectors"** section in the React UI, rendered below the plugin list.

- Same `PluginCard` component reused — accepts a `sink: Sink` prop (same shape as `Plugin`)
- `src/ui/src/types.ts`: add `Sink` type (same fields as `Plugin` + `isBuiltIn: boolean`)
- `src/ui/src/api.ts`: add `getSinks()`, `enableSink()`, `disableSink()`, `reloadSink()`, `getSinkConfig()`, `saveSinkConfig()`
- `App.tsx`: loads sinks alongside plugins, renders separate section with label "Output Connectors"

---

## Error Handling

- A sink that throws in `ConsumeAsync` logs the error; fan-out continues to remaining sinks
- A sink that throws in `StartAsync` is marked as not running; does not crash the service
- Disable calls `StopAsync` — if it throws, sink is forcibly removed from the active list anyway

---

## Testing

- `FakeSinkManager.cs` in `tests/Arrr.Tests/Support/` — mirrors `FakePluginManager`
- Unit tests in `tests/Arrr.Tests/Sinks/` for `DbusNotifySink` (mock D-Bus proxy) and `UnixSocketSink` (connect real socket, assert JSON line received)
- API endpoint tests in `tests/Arrr.Tests/Api/SinksEndpointTests.cs`
