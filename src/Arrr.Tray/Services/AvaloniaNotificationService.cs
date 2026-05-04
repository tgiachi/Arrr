using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Serilog;

namespace Arrr.Tray.Services;

/// <summary>
/// On Wayland apps cannot position their own windows, so we fall back to notify-send.
/// On X11 we spawn a custom borderless popup in the bottom-right corner.
/// </summary>
internal sealed class AvaloniaNotificationService : INotificationProvider
{
    private static readonly bool _isWayland =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private static int _stackOffset;
    private static readonly object _lock = new();

    public Task InitializeAsync()
    {
        Log.Information("Avalonia notification service ready (backend={Backend})",
            _isWayland ? "notify-send (Wayland)" : "popup (X11)");

        return Task.CompletedTask;
    }

    public Task ShowAsync(string title, string body, string? iconUrl = null, string? source = null, string? url = null)
    {
        if (_isWayland)
        {
            return ShowWaylandAsync(title, body, url);
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                int slot;
                lock (_lock) { slot = _stackOffset++; }

                var popup = BuildPopup(title, body, url);
                popup.Opacity = 0;
                popup.Show();
                PositionPopup(popup, slot);
                popup.Opacity = 1;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    popup.Close();
                    lock (_lock) { _stackOffset = Math.Max(0, _stackOffset - 1); }
                };
                timer.Start();

                Log.Debug("Avalonia popup shown: {Title}", title);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Avalonia notification failed");
            }
        });

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private static Task ShowWaylandAsync(string title, string body, string? url)
    {
        return Task.Run(() =>
        {
            try
            {
                // append URL to body so the user can see it even without click action
                var fullBody = url is not null ? $"{body}\n{url}" : body;

                var psi = new ProcessStartInfo("notify-send")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("--expire-time");
                psi.ArgumentList.Add("5000");
                psi.ArgumentList.Add("--app-name");
                psi.ArgumentList.Add("Arrr");

                if (url is not null)
                {
                    psi.ArgumentList.Add("--action");
                    psi.ArgumentList.Add($"default=Open");
                }

                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add(fullBody);

                using var proc = Process.Start(psi);

                if (proc is null)
                {
                    return;
                }

                // if --action is supported, wait for user choice (non-blocking with timeout)
                if (url is not null)
                {
                    var actionLine = "";
                    proc.WaitForExit(6000);
                    actionLine = proc.StandardOutput.ReadToEnd().Trim();

                    if (actionLine == "default")
                    {
                        try { Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false }); }
                        catch (Exception ex) { Log.Warning(ex, "xdg-open failed for {Url}", url); }
                    }
                }

                Log.Debug("notify-send sent: {Title}", title);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "notify-send failed");
            }
        });
    }

    private static void PositionPopup(Window window, int slot)
    {
        const int width = 340;
        const int height = 80;
        const int margin = 12;
        const int stackSpacing = height + 8;

        var workArea = window.Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var x = workArea.X + workArea.Width - width - margin;
        var y = workArea.Y + workArea.Height - height - margin - slot * stackSpacing;
        window.Position = new PixelPoint(x, y);
    }

    private static Window BuildPopup(string title, string body, string? url)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var bodyBlock = new TextBlock
        {
            Text = body,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2
        };

        var stack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 8)
        };
        stack.Children.Add(titleBlock);
        stack.Children.Add(bodyBlock);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 30, 30, 30)),
            CornerRadius = new CornerRadius(8),
            Child = stack,
            Cursor = url is not null ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) : null
        };

        if (url is not null)
        {
            border.PointerPressed += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false }); }
                catch (Exception ex) { Log.Warning(ex, "xdg-open failed for {Url}", url); }
            };
        }

        return new Window
        {
            Content = border,
            Width = 340,
            Height = 80,
            SystemDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            ShowInTaskbar = false,
            Topmost = true,
            CanResize = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual
        };
    }
}
