using System.Net;
using System.Net.Http.Json;
using Arrr.Core.Data.Api;
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
public class PluginsEndpointTests
{
    [Test]
    public async Task GetPlugins_WithNoPlugins_ReturnsEmptyArray()
    {
        var (client, app, _) = await CreateHostAsync("secret");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/plugins");
        request.Headers.Add("X-Api-Key", "secret");

        var response = await client.SendAsync(request);
        var plugins = await response.Content.ReadFromJsonAsync<PluginInfoResponse[]>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(plugins, Is.Empty);
    }

    [Test]
    public async Task GetPlugins_WithRegisteredPlugins_ReturnsPluginMetadata()
    {
        var (client, app, registry) = await CreateHostAsync("secret");
        await using var _ = app;

        registry.Register(new FakeSourcePlugin("com.test.alpha"));
        registry.Register(new FakeSourcePlugin("com.test.beta"));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/plugins");
        request.Headers.Add("X-Api-Key", "secret");

        var response = await client.SendAsync(request);
        var plugins = await response.Content.ReadFromJsonAsync<PluginInfoResponse[]>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(plugins, Has.Length.EqualTo(2));
        Assert.That(plugins!.Select(p => p.Id), Is.EquivalentTo(new[] { "com.test.alpha", "com.test.beta" }));
    }

    [Test]
    public async Task GetPlugins_WithMissingKey_Returns401()
    {
        var (client, app, _) = await CreateHostAsync("secret");
        await using var _ = app;

        var response = await client.GetAsync("/api/plugins");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetPlugins_WithWrongKey_Returns401()
    {
        var (client, app, _) = await CreateHostAsync("secret");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/plugins");
        request.Headers.Add("X-Api-Key", "wrong");

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetPlugins_WithEmptyApiKeyConfig_Returns503()
    {
        var (client, app, _) = await CreateHostAsync("");
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/plugins");
        request.Headers.Add("X-Api-Key", "anything");

        var response = await client.SendAsync(request);

        Assert.That((int)response.StatusCode, Is.EqualTo(503));
    }

    private static async Task<(HttpClient client, WebApplication app, IPluginRegistry registry)> CreateHostAsync(
        string apiKey)
    {
        var bus = new EventBusService();
        await bus.StartAsync(CancellationToken.None);

        var registry = new PluginRegistryService();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IConfigService>(new FakeConfigService(apiKey));
        builder.Services.AddSingleton<IEventBus>(bus);
        builder.Services.AddSingleton<IPluginRegistry>(registry);

        var app = builder.Build();
        app.MapPluginsApi();
        await app.StartAsync();

        return (app.GetTestClient(), app, registry);
    }
}
