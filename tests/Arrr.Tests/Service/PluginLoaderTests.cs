using Arrr.Service.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Service;

[TestFixture]
public class PluginLoaderTests
{
    [Test]
    public void Load_WhenPluginsDirectoryDoesNotExist_ReturnsEmptyList()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance, "/tmp/arrr_nonexistent_plugins");

        var result = loader.Load();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Load_WhenDirectoryIsEmpty_ReturnsEmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"arrr_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);

        try
        {
            var loader = new PluginLoader(NullLogger<PluginLoader>.Instance, dir);

            var result = loader.Load();

            Assert.That(result, Is.Empty);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void Load_WhenDirectoryHasNonDllFiles_ReturnsEmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"arrr_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "readme.txt"), "not a plugin");

        try
        {
            var loader = new PluginLoader(NullLogger<PluginLoader>.Instance, dir);

            var result = loader.Load();

            Assert.That(result, Is.Empty);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
