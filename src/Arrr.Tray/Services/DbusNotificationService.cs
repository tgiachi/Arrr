using System.Diagnostics;

namespace Arrr.Tray.Services;

internal sealed class DbusNotificationService
{
    private readonly bool _available;

    public DbusNotificationService()
    {
        // Check notify-send is reachable at startup; gracefully degrade if not
        _available = IsNotifySendAvailable();
    }

    public void Show(string title, string body)
    {
        if (!_available)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "notify-send",
                ArgumentList = { "--app-name=Arrr", "--expire-time=5000", title, body },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
        }
        catch
        {
            // notify-send unavailable at runtime — silently ignore
        }
    }

    private static bool IsNotifySendAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                ArgumentList = { "notify-send" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
            proc?.WaitForExit(1000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
