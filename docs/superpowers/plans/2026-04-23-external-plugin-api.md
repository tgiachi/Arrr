# External Plugin API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `POST /api/notify` HTTP endpoint that lets plugins written in any language publish notifications to Arrr via an API key-protected HTTP call, routing them through the existing `IEventBus` pipeline.

**Architecture:** A new `ExternalNotifyEndpoint` static class registers a single minimal API route via `MapExternalApi()` extension method. The endpoint validates `X-Api-Key`, builds a `Notification`, and calls `IEventBus.PublishAsync` — both Unix socket and D-Bus subscribers receive the notification automatically. A new `ApiKey` property in `ArrrConfig` controls access; empty string disables the endpoint with `503`.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, NUnit 4.x, `Microsoft.AspNetCore.Mvc.Testing` (for in-process test host).

---

### Task 1: ExternalNotifyRequest DTO + ApiKey config

**Files:**
- Create: `src/Arrr.Core/Data/Api/ExternalNotifyRequest.cs`
- Modify: `src/Arrr.Core/Data/Config/ArrrConfig.cs`

- [ ] **Step 1: Create ExternalNotifyRequest DTO**

Create `src/Arrr.Core/Data/Api/ExternalNotifyRequest.cs`:

```csharp
namespace Arrr.Core.Data.Api;

public record ExternalNotifyRequest(
    string Source,
    string Title,
    string Body,
    string? IconUrl
);
```

- [ ] **Step 2: Add ApiKey to ArrrConfig**

Edit `src/Arrr.Core/Data/Config/ArrrConfig.cs`:

```csharp
namespace Arrr.Core.Data.Config;

public class ArrrConfig
{
    public string SocketPath { get; set; } = "/tmp/arrr.sock";
    public string ApiKey { get; set; } = "";
    public ArrrWebConfig Web { get; set; } = new();
    public List<PluginEntry> Plugins { get; set; } = [];
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Arrr.Core/Arrr.Core.csproj`

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Arrr.Core/Data/Api/ExternalNotifyRequest.cs src/Arrr.Core/Data/Config/ArrrConfig.cs
git commit -m "feat(api): add ExternalNotifyRequest DTO and ApiKey to ArrrConfig"
```

---

### Task 2: Test infrastructure + failing tests

**Files:**
- Modify: `tests/Arrr.Tests/Arrr.Tests.csproj`
- Create: `tests/Arrr.Tests/Support/FakeConfigService.cs`
- Create: `tests/Arrr.Tests/Service/ExternalNotifyEndpointTests.cs`

- [ ] **Step 1: Add Microsoft.AspNetCore.Mvc.Testing to test project**

Edit `tests/Arrr.Tests/Arrr.Tests.csproj` — add inside the existing packages `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0"/>
```

- [ ] **Step 2: Restore packages**

Run: `dotnet restore tests/Arrr.Tests/Arrr.Tests.csproj`

Expected: restored without errors.

- [ ] **Step 3: Create FakeConfigService**

Create `tests/Arrr.Tests/Support/FakeConfigService.cs`:

```csharp
using Arrr.Core.Data.Config;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeConfigService : IConfigService
{
    public ArrrConfig Config { get; }

    public FakeConfigService(string apiKey = "")
    {
        Config = new ArrrConfig { ApiKey = apiKey };
    }

    public Task LoadAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Write failing tests**

Create `tests/Arrr.Tests/Service/ExternalNotifyEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Services;
using Arrr.Service.Api;
using Arrr.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Arrr.Tests.Service;

[TestFixture]
public class ExternalNotifyEndpointTests
{
    private async Task<(HttpClient client, WebApplication app, EventBusService bus)> CreateHostAsync(
        string apiKey)
    {
        var bus = new EventBusService();
        await bus.StartAsync(CancellationToken.None);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IConfigService>(new FakeConfigService(apiKey));
        builder.Services.AddSingleton<IEventBus>(bus);

        var app = builder.Build();
        app.MapExternalApi();
        await app.StartAsync();

        return (app.GetTestClient(), app, bus);
    }

    [Test]
    public async Task Notify_WithValidKeyAndPayload_Returns204()
    {
        var (client, app, _) = await CreateHostAsync("secret");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notify");
        request.Headers.Add("X-Api-Key", "secret");
        request.Content = JsonContent.Create(new ExternalNotifyRequest("test-bot", "Title", "Body", null));

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Notify_WithWrongKey_Returns401()
    {
        var (client, app, _) = await CreateHostAsync("secret");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notify");
        request.Headers.Add("X-Api-Key", "wrong");
        request.Content = JsonContent.Create(new ExternalNotifyRequest("bot", "T", "B", null));

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Notify_WithMissingKey_Returns401()
    {
        var (client, app, _) = await CreateHostAsync("secret");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notify");
        request.Content = JsonContent.Create(new ExternalNotifyRequest("bot", "T", "B", null));

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Notify_WithEmptyApiKeyConfig_Returns503()
    {
        var (client, app, _) = await CreateHostAsync("");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notify");
        request.Headers.Add("X-Api-Key", "anything");
        request.Content = JsonContent.Create(new ExternalNotifyRequest("bot", "T", "B", null));

        var response = await client.SendAsync(request);

        Assert.That((int)response.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public async Task Notify_WithMissingTitle_Returns400()
    {
        var (client, app, _) = await CreateHostAsync("secret");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notify");
        request.Headers.Add("X-Api-Key", "secret");
        request.Content = JsonContent.Create(new ExternalNotifyRequest("bot", "", "Body", null));

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Notify_WhenPublished_EventBusReceivesNotification()
    {
        var (client, app, bus) = await CreateHostAsync("secret");
        await using var _ = app;

        var received = new TaskCompletionSource<Notification>();
        bus.Subscribe<Notification>((n, ct) =>
        {
            received.TrySetResult(n);
            return Task.CompletedTask;
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notify");
        request.Headers.Add("X-Api-Key", "secret");
        request.Content = JsonContent.Create(
            new ExternalNotifyRequest("my-bot", "Hello", "World", "https://example.com/icon.png"));

        await client.SendAsync(request);

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(notification.Source, Is.EqualTo("my-bot"));
        Assert.That(notification.Title, Is.EqualTo("Hello"));
        Assert.That(notification.Body, Is.EqualTo("World"));
        Assert.That(notification.IconUrl, Is.EqualTo("https://example.com/icon.png"));
    }
}
```

- [ ] **Step 5: Run tests — expect compile error (ExternalNotifyEndpoint not yet created)**

Run: `dotnet test tests/Arrr.Tests/Arrr.Tests.csproj --filter "ExternalNotifyEndpoint" -v n 2>&1 | tail -10`

Expected: compile error — `MapExternalApi` does not exist.

- [ ] **Step 6: Commit test infrastructure**

```bash
git add tests/Arrr.Tests/Arrr.Tests.csproj tests/Arrr.Tests/Support/FakeConfigService.cs tests/Arrr.Tests/Service/ExternalNotifyEndpointTests.cs
git commit -m "test(api): add ExternalNotifyEndpoint tests and FakeConfigService"
```

---

### Task 3: Implement ExternalNotifyEndpoint + wire in Program.cs

**Files:**
- Create: `src/Arrr.Service/Api/ExternalNotifyEndpoint.cs`
- Modify: `src/Arrr.Service/Program.cs`

- [ ] **Step 1: Create ExternalNotifyEndpoint**

Create `src/Arrr.Service/Api/ExternalNotifyEndpoint.cs`:

```csharp
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class ExternalNotifyEndpoint
{
    public static IEndpointRouteBuilder MapExternalApi(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/notify",
            async (HttpContext ctx, ExternalNotifyRequest req, IConfigService configService, IEventBus eventBus) =>
            {
                var key = configService.Config.ApiKey;

                if (key == "")
                    return Results.Problem("API key not configured", statusCode: 503);

                if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != key)
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(req.Source) ||
                    string.IsNullOrWhiteSpace(req.Title)  ||
                    string.IsNullOrWhiteSpace(req.Body))
                    return Results.BadRequest("source, title and body are required");

                var notification = new Notification(
                    Guid.NewGuid(),
                    req.Source,
                    req.Title,
                    req.Body,
                    DateTimeOffset.UtcNow,
                    req.IconUrl
                );

                await eventBus.PublishAsync(notification);
                return Results.NoContent();
            }
        );

        return app;
    }
}
```

- [ ] **Step 2: Wire MapExternalApi in Program.cs**

In `src/Arrr.Service/Program.cs`, add `using Arrr.Service.Api;` at the top (with the other using directives), then add the following line after `app.Services.GetRequiredService<SocketBroadcastSubscriber>();`:

```csharp
app.MapExternalApi();
```

The relevant block in Program.cs becomes:

```csharp
using Arrr.Service.Api;
// ... existing usings ...

// after app is built:
app.Services.GetRequiredService<SocketBroadcastSubscriber>();
app.MapExternalApi();

app.MapGet("/callback/{pluginName}", ...);
app.MapPost("/callback/{pluginName}", ...);
```

- [ ] **Step 3: Run targeted tests**

Run: `dotnet test tests/Arrr.Tests/Arrr.Tests.csproj --filter "ExternalNotifyEndpoint" -v n 2>&1 | tail -15`

Expected: 6 tests pass.

- [ ] **Step 4: Run all tests**

Run: `dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -v n 2>&1 | tail -10`

Expected: all tests pass, 0 failures.

- [ ] **Step 5: Build full solution**

Run: `dotnet build src/Arrr.Service/Arrr.Service.csproj 2>&1 | tail -8`

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/Arrr.Service/Api/ExternalNotifyEndpoint.cs src/Arrr.Service/Program.cs
git commit -m "feat(api): implement POST /api/notify external plugin endpoint"
```
