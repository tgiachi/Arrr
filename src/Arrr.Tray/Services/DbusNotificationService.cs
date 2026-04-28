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
}

internal sealed class DbusNotificationService : IAsyncDisposable
{
    private static readonly HttpClient _http = new();

    private Connection? _connection;
    private IFreedesktopNotifications? _proxy;
    private bool _available;

    private IReadOnlyDictionary<string, byte[]> _iconCache = new Dictionary<string, byte[]>();
    private readonly Dictionary<string, string> _iconTempFiles = new();

    public void SetIconCache(IReadOnlyDictionary<string, byte[]> cache)
    {
        // Clean up old temp files before replacing the cache
        foreach (var path in _iconTempFiles.Values)
            try { File.Delete(path); } catch { /* best-effort */ }

        _iconTempFiles.Clear();
        _iconCache = cache;

        // Pre-write all cached icons to temp files for fast D-Bus access
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
            _available = true;
            Log.Information("D-Bus notification service ready");
        }
        catch (Exception ex)
        {
            _available = false;
            Log.Warning(ex, "D-Bus notification service unavailable");
        }
    }

    public async Task ShowAsync(string title, string body, string? iconUrl = null, string? source = null)
    {
        if (!_available || _proxy is null)
        {
            Log.Warning("D-Bus ShowAsync called but service not available");
            return;
        }

        // If no explicit iconUrl but we have a cached icon for the source plugin, use it
        var effectiveIconUrl = iconUrl;
        if (string.IsNullOrEmpty(effectiveIconUrl) && source is not null && _iconTempFiles.TryGetValue(source, out var cachedPath))
            effectiveIconUrl = cachedPath;

        var (appIcon, tempFile) = await ResolveIconAsync(effectiveIconUrl);
        try
        {
            Log.Debug("D-Bus: sending notification title={Title} icon={Icon}", title, appIcon);
            await _proxy.NotifyAsync(
                "Arrr", 0, appIcon,
                title, body,
                [], new Dictionary<string, object>(), 5000);
            Log.Debug("D-Bus: notification sent");
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

        // file URI or absolute path — pass directly
        if (iconUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            Path.IsPathRooted(iconUrl))
            return (iconUrl, null);

        // HTTP/HTTPS — download to a temp file
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

        // treat as icon theme name
        return (iconUrl, null);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var path in _iconTempFiles.Values)
            try { File.Delete(path); } catch { /* best-effort */ }
        _iconTempFiles.Clear();

        _connection?.Dispose();
        _connection = null;
        _proxy = null;
        _available = false;
    }
}
