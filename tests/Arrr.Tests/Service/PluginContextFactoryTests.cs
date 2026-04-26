using Arrr.Service.Internal;
using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class PluginContextFactoryTests
{
    private string _tempRoot = "";
    private DirectoriesConfig _directoriesConfig = null!;

    [Test]
    public void Create_CallbackUrl_ContainsPluginName()
    {
        var factory = new PluginContextFactory(new EventBusService(), _directoriesConfig);
        var plugin = new FakeSourcePlugin("com.test.arrr.plugins.rss");

        var ctx = factory.Create(plugin);

        Assert.That(ctx.CallbackUrl, Does.Contain("rss"));
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

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"arrr_ctx_test_{Guid.NewGuid()}");
        _directoriesConfig = new(_tempRoot, Enum.GetNames<DirectoryType>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }
}
