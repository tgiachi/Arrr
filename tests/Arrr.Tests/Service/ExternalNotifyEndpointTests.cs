using System.Net;
using System.Net.Http.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Services;
using Arrr.Service.Api;
using Arrr.Service.Services;
using Arrr.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Arrr.Tests.Service;

[TestFixture]
public class ExternalNotifyEndpointTests
{
    [Test]
    public async Task Notify_WhenPublished_EventBusReceivesNotification()
    {
        var (client, app, bus) = await CreateHostAsync("secret");
        await using var _ = app;

        var received = new TaskCompletionSource<Notification>();
        bus.Subscribe<Notification>(
            (n, ct) =>
            {
                received.TrySetResult(n);

                return Task.CompletedTask;
            }
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notify");
        request.Headers.Add("X-Api-Key", "secret");
        request.Content = JsonContent.Create(
            new ExternalNotifyRequest("my-bot", "Hello", "World", "https://example.com/icon.png")
        );

        await client.SendAsync(request);

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(notification.Source, Is.EqualTo("my-bot"));
        Assert.That(notification.Title, Is.EqualTo("Hello"));
        Assert.That(notification.Body, Is.EqualTo("World"));
        Assert.That(notification.IconUrl, Is.EqualTo("https://example.com/icon.png"));
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

    private async Task<(HttpClient client, WebApplication app, EventBusService bus)> CreateHostAsync(string apiKey)
    {
        var bus = new EventBusService();
        await bus.StartAsync(CancellationToken.None);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IConfigService>(new FakeConfigService(apiKey));
        builder.Services.AddSingleton<IEventBus>(bus);
        builder.Services.AddSingleton<IPluginRegistry, PluginRegistryService>();

        var app = builder.Build();
        app.MapExternalApi();
        await app.StartAsync();

        return (app.GetTestClient(), app, bus);
    }
}
