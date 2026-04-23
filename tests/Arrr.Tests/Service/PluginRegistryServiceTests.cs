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
