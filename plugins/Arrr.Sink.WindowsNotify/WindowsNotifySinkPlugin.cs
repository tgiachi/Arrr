using Arrr.Sink.WindowsNotify.Data;

namespace Arrr.Sink.WindowsNotify;

public class WindowsNotifySinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private WindowsNotifyConfig _config = new();
    private ISinkContext? _context;
    private bool _initialized;

    public string Id => "com.arrr.sink.windows-notify";
    public string Name => "Windows Notifications";
    public string Version => VersionUtils.Get(typeof(WindowsNotifySinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Delivers notifications as native Windows toast notifications via the Action Center.";
    public string Icon => "🪟";
    public Type ConfigType => typeof(WindowsNotifyConfig);
    public PlatformType[] Platforms => [PlatformType.Windows];

    public Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (!_initialized)
        {
            return Task.CompletedTask;
        }

        try
        {
            var builder = new ToastContentBuilder()
                          .AddText(notification.Title)
                          .AddText(notification.Body);

            if (_config.ShowSource)
            {
                builder.AddAttributionText(notification.Source);
            }

            if (!string.IsNullOrEmpty(notification.IconUrl) &&
                Uri.TryCreate(notification.IconUrl, UriKind.Absolute, out var iconUri))
            {
                builder.AddAppLogoOverride(iconUri);
            }

            if (notification.Priority == NotificationPriority.Critical)
            {
                builder.SetToastScenario(ToastScenario.Alarm);
            }
            else if (notification.Priority == NotificationPriority.High)
            {
                builder.SetToastScenario(ToastScenario.Reminder);
            }

            builder.Show();
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Windows notification failed: {Title}", notification.Title);
        }

        return Task.CompletedTask;
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<WindowsNotifyConfig>(ct);

        try
        {
            // Registering OnActivated is required for unpackaged desktop apps —
            // it writes the AUMID entry to the registry on first run.
            ToastNotificationManagerCompat.OnActivated += _ => { };
            _initialized = true;
            context.Logger.LogInformation("Windows notification sink ready");
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Windows notification sink unavailable");
            _initialized = false;
        }
    }

    public Task StopAsync()
    {
        _initialized = false;

        return Task.CompletedTask;
    }
}
