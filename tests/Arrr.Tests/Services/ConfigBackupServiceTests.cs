using System.Text.Json;
using Arrr.Core.Directories;
using Arrr.Core.Types;
using Arrr.Service.Internal;

namespace Arrr.Tests.Services;

[TestFixture]
public class ConfigBackupServiceTests
{
    private string _tempRoot = "";
    private DirectoriesConfig _directoriesConfig = null!;
    private ConfigBackupService _service = null!;

    [Test]
    public async Task ExportAsync_ReadsAllConfigFiles()
    {
        var configsDir = _directoriesConfig[DirectoryType.Configs];
        await File.WriteAllTextAsync(Path.Combine(configsDir, "com.arrr.sink.ntfy.config"), """{"topic":"alerts"}""");
        await File.WriteAllTextAsync(Path.Combine(configsDir, "com.arrr.rss.config"), """{"feeds":[]}""");

        var result = await _service.ExportAsync(CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.ContainsKey("com.arrr.sink.ntfy"), Is.True);
        Assert.That(result.ContainsKey("com.arrr.rss"), Is.True);
        Assert.That(result["com.arrr.sink.ntfy"].GetProperty("topic").GetString(), Is.EqualTo("alerts"));
    }

    [Test]
    public async Task ImportAsync_WritesConfigFiles()
    {
        var configs = new Dictionary<string, JsonElement>
        {
            ["com.arrr.sink.smtp"] = JsonDocument.Parse("""{"from":"a@b.com"}""").RootElement,
            ["com.arrr.rss"] = JsonDocument.Parse("""{"feeds":[]}""").RootElement
        };

        var count = await _service.ImportAsync(configs, CancellationToken.None);

        Assert.That(count, Is.EqualTo(2));
        var configsDir = _directoriesConfig[DirectoryType.Configs];
        Assert.That(File.Exists(Path.Combine(configsDir, "com.arrr.sink.smtp.config")), Is.True);
        Assert.That(File.Exists(Path.Combine(configsDir, "com.arrr.rss.config")), Is.True);
        var written = await File.ReadAllTextAsync(Path.Combine(configsDir, "com.arrr.sink.smtp.config"));
        Assert.That(written, Does.Contain("a@b.com"));
    }

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"arrr_backup_test_{Guid.NewGuid()}");
        _directoriesConfig = new(_tempRoot, Enum.GetNames<DirectoryType>());
        _service = new ConfigBackupService(_directoriesConfig);
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
