using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Arrr.Tray.Services;
using Arrr.Tray.ViewModels;
using Serilog;

namespace Arrr.Tray;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Log.Debug("OnFrameworkInitializationCompleted — lifetime={LifetimeType}", ApplicationLifetime?.GetType().Name ?? "null");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            Log.Information("Settings loaded — url={Url} apiKeySet={ApiKeySet}", settings.ServerUrl, !string.IsNullOrEmpty(settings.ApiKey));

            var grpc = new ArrrGrpcClient();
            grpc.Connect(settings.ServerUrl, settings.ApiKey, settings.GrpcUrl);
            Log.Information("gRPC client connected to {Url}", settings.ServerUrl);

            var vm = new TrayViewModel(grpc, settingsService);

            var versionItem = new NativeMenuItem("Arrr");

            var statusItem = new NativeMenuItem("Disconnected")
            {
                Icon = LoadBitmap("button_red.png"),
            };

            var dndItem = new NativeMenuItem(vm.DndLabel);
            dndItem.Click += (_, _) =>
            {
                Log.Debug("Menu click: DND toggle");
                _ = vm.ToggleDndCommand.ExecuteAsync(null);
            };

            var settingsItem = new NativeMenuItem("Settings");
            settingsItem.Click += (_, _) =>
            {
                Log.Debug("Menu click: Settings");
                vm.OpenSettingsCommand.Execute(null);
            };

            var exitItem = new NativeMenuItem("Exit");
            exitItem.Click += (_, _) =>
            {
                Log.Debug("Menu click: Exit");
                vm.ExitCommand.Execute(null);
            };

            var menu = new NativeMenu();
            menu.Items.Add(versionItem);
            menu.Items.Add(statusItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(dndItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exitItem);

            var trayIcon = new TrayIcon
            {
                Icon = LoadIcon("tray-icon.png"),
                ToolTipText = "Arrr",
                Menu = menu,
            };

            vm.DndStateChanged += enabled =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    dndItem.Header = vm.DndLabel;
                    trayIcon.Icon = LoadIcon(enabled ? "tray-icon-dnd.png" : "tray-icon.png");
                });
            };

            vm.ServerVersionChanged += ver =>
            {
                Dispatcher.UIThread.Post(() => versionItem.Header = ver);
            };

            vm.ConnectionStateChanged += connected =>
            {
                Log.Debug("Connection state changed → {State}", connected ? "Connected" : "Disconnected");
                Dispatcher.UIThread.Post(() =>
                {
                    statusItem.Header = connected ? "Connected" : "Disconnected";
                    statusItem.Icon   = LoadBitmap(connected ? "button_green.png" : "button_red.png");
                });
            };

            TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
            Log.Debug("TrayIcon registered");

            var dbus = new DbusNotificationService();
            _ = dbus.InitializeAsync();

            grpc.NotificationReceived += notif =>
            {
                Log.Debug("Notification received from {Source}: {Title}", notif.Source, notif.Title);
                if (!vm.DndEnabled)
                    _ = dbus.ShowAsync(notif.Title, notif.Body);
            };

            _ = vm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static WindowIcon LoadIcon(string fileName)
    {
        var uri = new Uri($"avares://arrr-tray/Assets/{fileName}");
        return new WindowIcon(AssetLoader.Open(uri));
    }

    private static Bitmap LoadBitmap(string fileName)
    {
        var uri = new Uri($"avares://arrr-tray/Assets/{fileName}");
        var source = new Bitmap(AssetLoader.Open(uri));
        return source.CreateScaledBitmap(new Avalonia.PixelSize(20, 20), BitmapInterpolationMode.HighQuality);
    }
}
