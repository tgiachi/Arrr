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
        var (client, app, _) = await CreateHostAsync("secret", [sinkA]);
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
