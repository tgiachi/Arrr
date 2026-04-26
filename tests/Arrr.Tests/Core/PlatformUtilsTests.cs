using Arrr.Core.Utils;

namespace Arrr.Tests.Core;

[TestFixture]
public class PlatformUtilsTests
{
    [Test]
    public void IsCompatible_EmptyArray_ReturnsTrue()
    {
        Assert.That(PlatformUtils.IsCompatible([]), Is.True);
    }

    [Test]
    public void IsCompatible_UnknownPlatform_ReturnsFalse()
    {
        Assert.That(PlatformUtils.IsCompatible(["Haiku"]), Is.False);
    }

    [Test]
    public void IsCompatible_CurrentPlatform_ReturnsTrue()
    {
        var current = OperatingSystem.IsLinux() ? "Linux"
                    : OperatingSystem.IsWindows() ? "Windows"
                    : "OSX";

        Assert.That(PlatformUtils.IsCompatible([current]), Is.True);
    }

    [Test]
    public void IsCompatible_MultipleIncludingCurrent_ReturnsTrue()
    {
        Assert.That(PlatformUtils.IsCompatible(["Linux", "Windows", "OSX"]), Is.True);
    }

    [Test]
    public void IsCompatible_PlatformNameIsCaseInsensitive()
    {
        var current = OperatingSystem.IsLinux() ? "linux"
                    : OperatingSystem.IsWindows() ? "windows"
                    : "osx";

        Assert.That(PlatformUtils.IsCompatible([current]), Is.True);
    }

    [Test]
    public void SystemdPlugin_HasLinuxPlatform()
    {
        var plugin = new Arrr.Plugin.Systemd.SystemdJournalPlugin();
        Assert.That(plugin.Platforms, Is.EquivalentTo(new[] { "Linux" }));
    }
}
