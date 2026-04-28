# Writing a Plugin

Implement `ISourcePlugin` from the `Arrr.Core` NuGet package, drop the compiled `.dll` into the `plugins/` directory and restart the daemon (or use `POST /api/plugins/reload/all` to hot-reload).

## Minimal example

```csharp
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;

public class MyPlugin : ISourcePlugin
{
    public string Id          => "com.example.myplugin";
    public string Name        => "My Plugin";
    public string Version     => VersionUtils.Get(typeof(MyPlugin));
    public string Author      => "Your Name";
    public string Description => "Fetches something and sends notifications";
    public string[] Categories => ["example"];
    public string Icon        => "";

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await context.EventBus.PublishAsync(
                new Notification(Guid.NewGuid(), Id, "Hello", "World", DateTimeOffset.UtcNow, null),
                ct
            );
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

## IPluginContext

| Member | Description |
|--------|-------------|
| `EventBus` | Publish notifications and subscribe to internal events |
| `Http` | Shared `HttpClient`; one instance per plugin, safe to use directly |
| `Logger` | Scoped Serilog logger |
| `LoadConfigAsync<T>()` | Load typed per-plugin config (from `configs/<id>.config`) |
| `SaveConfigAsync<T>()` | Persist typed per-plugin config |

## Optional interfaces

| Interface | Purpose |
|-----------|---------|
| `IPollingPlugin` | Declare a poll interval; the host calls `PollAsync` on schedule |
| `IConfigurablePlugin<T>` | Typed config loaded via `context.LoadConfigAsync<T>()` |
| `ICallbackPlugin` | Receive HTTP callbacks at `POST /api/plugins/{id}/callback` |
| `IQrPlugin` | Surface a QR code in the web UI for first-time pairing flows |
| `ITestablePlugin` | Expose a "Test connection" button in the web UI config modal |

## Plugin icon

Embed a file named `icon.png` in the assembly as an embedded resource with `LogicalName="icon.png"`. The daemon picks it up automatically and serves it via `/api/plugins/{id}/icon`. If not present, the Arrr logo is used as fallback.

```xml
<ItemGroup>
  <EmbeddedResource Include="icon.png" LogicalName="icon.png" />
</ItemGroup>
```

## Project template

```bash
dotnet new install Arrr.Templates
dotnet new arrr-plugin -n MyPlugin \
    --PluginId com.example.myplugin \
    --Author "Your Name" \
    --Interval "00:05:00"
```

## Sensitive config fields

Mark fields that contain secrets with `[Sensitive]` — the daemon encrypts them at rest using AES:

```csharp
public class MyConfig
{
    public string ApiUrl { get; set; } = "";

    [Sensitive]
    public string ApiKey { get; set; } = "";
}
```

## Writing a sink plugin

Implement `ISinkPlugin` instead of `ISourcePlugin`. The `ISinkContext` interface mirrors `IPluginContext`. Sink plugins receive a `Notification` for each event that passes routing rules and the DND check.

```csharp
public class MySink : ISinkPlugin
{
    public string Id => "com.example.mysink";
    // ...

    public async Task SendAsync(Notification notification, ISinkContext context, CancellationToken ct)
    {
        // deliver the notification somewhere
    }
}
```
