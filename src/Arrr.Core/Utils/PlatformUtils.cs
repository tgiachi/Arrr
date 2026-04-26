using Arrr.Core.Types;

namespace Arrr.Core.Utils;

/// <summary>
/// Provides utilities for detecting the current platform.
/// </summary>
public static class PlatformUtils
{
    /// <summary>
    /// Gets the current platform type.
    /// </summary>
    /// <returns>The detected platform type.</returns>
    public static PlatformType GetCurrentPlatform()
    {
        if (IsRunningOnWindows())
        {
            return PlatformType.Windows;
        }

        if (IsRunningOnMacOS())
        {
            return PlatformType.Osx;
        }

        return IsRunningOnLinux() ? PlatformType.Linux : PlatformType.Unknown;
    }

    /// <summary>
    /// Checks if the application is running inside a Docker container.
    /// </summary>
    /// <returns></returns>
    public static bool IsRunningFromDocker()
        => Environment.GetEnvironmentVariable("MOONGATE_IS_DOCKER") == "true";

    /// <summary>
    /// Checks if the application is running on Linux.
    /// </summary>
    /// <returns>True if running on Linux, otherwise false.</returns>
    public static bool IsRunningOnLinux()
        => OperatingSystem.IsLinux();

    /// <summary>
    /// Checks if the application is running on macOS.
    /// </summary>
    /// <returns>True if running on macOS, otherwise false.</returns>
    public static bool IsRunningOnMacOS()
        => OperatingSystem.IsMacOS();

    /// <summary>
    /// Checks if the application is running on Windows.
    /// </summary>
    /// <returns>True if running on Windows, otherwise false.</returns>
    public static bool IsRunningOnWindows()
        => OperatingSystem.IsWindows();

    /// <summary>
    /// Returns true if the current OS matches at least one of the specified platforms,
    /// or if the array is empty (meaning all platforms are supported).
    /// </summary>
    public static bool IsCompatible(PlatformType[] platforms)
    {
        if (platforms.Length == 0)
        {
            return true;
        }

        return platforms.Contains(GetCurrentPlatform());
    }
}
