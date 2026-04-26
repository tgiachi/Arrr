using Arrr.Core.Types;
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
        Assert.That(PlatformUtils.IsCompatible([PlatformType.Unknown]), Is.False);
    }

    [Test]
    public void IsCompatible_CurrentPlatform_ReturnsTrue()
    {
        var current = PlatformUtils.GetCurrentPlatform();
        Assert.That(PlatformUtils.IsCompatible([current]), Is.True);
    }

    [Test]
    public void IsCompatible_AllPlatforms_ReturnsTrue()
    {
        Assert.That(
            PlatformUtils.IsCompatible([PlatformType.Linux, PlatformType.Windows, PlatformType.Osx]),
            Is.True
        );
    }

    [Test]
    public void SystemdPlugin_HasLinuxPlatform()
    {
        var plugin = new Arrr.Plugin.Systemd.SystemdJournalPlugin();
        Assert.That(plugin.Platforms, Is.EquivalentTo(new[] { PlatformType.Linux }));
    }
}
