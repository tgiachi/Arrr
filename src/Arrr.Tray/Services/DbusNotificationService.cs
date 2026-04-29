using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog;
using Tmds.DBus;

namespace Arrr.Tray.Services;

[DBusInterface("org.freedesktop.Notifications")]
public interface IFreedesktopNotifications : IDBusObject
{
    Task<uint> NotifyAsync(
        string appName,
        uint replacesId,
        string appIcon,
        string summary,
        string body,
        string[] actions,
        IDictionary<string, object> hints,
        int expireTimeout);

    Task<IDisposable> WatchActionInvokedAsync(
        Action<(uint Id, string ActionKey)> handler,
        Action<Exception>? onError = null);
}

internal sealed class DbusNotificationService : INotificationProvider, IAsyncDisposable
{
    private static readonly HttpClient _http = new();

    private readonly ConcurrentDictionary<uint, string> _pendingUrls = new();

    private Connection? _connection;
    private IFreedesktopNotifications? _proxy;
    private IDisposable? _actionSub;
    private bool _available;

    private IReadOnlyDictionary<string, byte[]> _iconCache = new Dictionary<string, byte[]>();
    private readonly Dictionary<string, string> _iconTempFiles = new();

    public void SetIconCache(IReadOnlyDictionary<string, byte[]> cache)
    {
        foreach (var path in _iconTempFiles.Values)
            try { File.Delete(path); } catch { /* best-effort */ }

        _iconTempFiles.Clear();
        _iconCache = cache;

        foreach (var (id, bytes) in cache)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"arrr-icon-{id.Replace('.', '-')}.png");
                File.WriteAllBytes(path, bytes);
                _iconTempFiles[id] = path;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write icon temp file for {Id}", id);
            }
        }

        Log.Debug("D-Bus icon cache set: {Count} icons pre-written to temp", _iconTempFiles.Count);
    }

    public async Task InitializeAsync()
    {
        try
        {
            Log.Debug("D-Bus: connecting to session bus (Address={Addr})", Address.Session);
            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();
            Log.Debug("D-Bus: connected");

            _proxy = _connection.CreateProxy<IFreedesktopNotifications>(
                "org.freedesktop.Notifications",
                "/org/freedesktop/Notifications");

            _actionSub = await _proxy.WatchActionInvokedAsync(
                e =>
                {
                    if (e.ActionKey != "default")
                    {
                        return;
                    }

                    if (!_pendingUrls.TryRemove(e.Id, out var url))
                    {
                        return;
                    }

                    try
                    {
                        Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
                        Log.Debug("xdg-open: {Url}", url);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "xdg-open failed for {Url}", url);
                    }
                },
                ex => Log.Warning(ex, "ActionInvoked signal error")
            );

            _available = true;
            Log.Information("D-Bus notification service ready");
        }
        catch (Exception ex)
        {
            _available = false;
            Log.Warning(ex, "D-Bus notification service unavailable");
        }
    }

    public async Task ShowAsync(string title, string body, string? iconUrl = null, string? source = null, string? url = null)
    {
        if (!_available || _proxy is null)
        {
            Log.Warning("D-Bus ShowAsync called but service not available");
            return;
        }

        var effectiveIconUrl = iconUrl;
        if (string.IsNullOrEmpty(effectiveIconUrl) && source is not null && _iconTempFiles.TryGetValue(source, out var cachedPath))
            effectiveIconUrl = cachedPath;

        var (appIcon, tempFile) = await ResolveIconAsync(effectiveIconUrl);
        try
        {
            var actions = url is not null
                ? new[] { "default", "Open" }
                : Array.Empty<string>();

            Log.Debug("D-Bus: sending notification title={Title} icon={Icon}", title, appIcon);
            var id = await _proxy.NotifyAsync(
                "Arrr", 0, appIcon,
                title, body,
                actions, new Dictionary<string, object>(), 5000);

            if (url is not null)
            {
                _pendingUrls[id] = url;
            }

            Log.Debug("D-Bus: notification sent id={Id}", id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "D-Bus notification failed");
        }
        finally
        {
            if (tempFile is not null)
                try { File.Delete(tempFile); } catch { /* best-effort */ }
        }
    }

    private static async Task<(string AppIcon, string? TempFile)> ResolveIconAsync(string? iconUrl)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
            return ("dialog-information", null);

        if (iconUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            Path.IsPathRooted(iconUrl))
            return (iconUrl, null);

        if (iconUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            iconUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var ext = Path.GetExtension(new Uri(iconUrl).AbsolutePath);
                if (string.IsNullOrEmpty(ext)) ext = ".png";

                var tempFile = Path.Combine(Path.GetTempPath(), $"arrr-icon-{Guid.NewGuid():N}{ext}");
                var bytes = await _http.GetByteArrayAsync(iconUrl);
                await File.WriteAllBytesAsync(tempFile, bytes);
                Log.Debug("D-Bus: icon downloaded to {TempFile}", tempFile);
                return (tempFile, tempFile);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "D-Bus: failed to download icon {Url}, falling back to default", iconUrl);
                return ("dialog-information", null);
            }
        }

        return (iconUrl, null);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var path in _iconTempFiles.Values)
            try { File.Delete(path); } catch { /* best-effort */ }
        _iconTempFiles.Clear();

        _actionSub?.Dispose();
        _actionSub = null;
        _connection?.Dispose();
        _connection = null;
        _proxy = null;
        _available = false;
    }
}
