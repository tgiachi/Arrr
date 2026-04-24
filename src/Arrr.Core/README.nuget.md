# Arrr.Core

Core SDK for writing plugins for the [Arrr](https://github.com/tgiachi/Arrr) notification aggregator daemon.

## Install

```
dotnet add package Arrr.Core
```

## Quick start — polling plugin

```csharp
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

public class MyPlugin : IPollingPlugin
{
    public string Id          => "com.example.myplugin";
    public string Name        => "My Plugin";
    public string Version     => "1.0.0";
    public string Author      => "Your Name";
    public string Description => "Fetches something every 5 minutes";
    public string[] Categories => ["example"];
    public string Icon        => "";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public Task StartAsync(IPluginContext context, CancellationToken ct) => Task.CompletedTask;

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        await context.EventBus.PublishAsync(
            new Notification(Guid.NewGuid(), Id, "Hello", "World", DateTimeOffset.UtcNow, null),
            ct
        );
    }
}
```

Drop the compiled `.dll` into Arrr's `plugins/` directory and add the plugin entry to `arrr.config`.

## Interfaces

| Interface | Use when |
|-----------|----------|
| `IPollingPlugin` | Fixed-interval polling — service manages the loop |
| `ISourcePlugin` | Custom scheduling / event-driven sources |
