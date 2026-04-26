# Arrr! ☠️

<p align="center">
  <img src="assets/arr_logo.png" alt="Arrr! logo" width="128"/>
</p>

> *Arrr!* — because every good notification deserves a pirate's welcome.

Arrr is a Linux desktop notification aggregator daemon. It runs as a background service, collects notifications from multiple sources via a plugin system, and delivers them to your desktop through D-Bus and configurable sink plugins. A built-in web UI lets you browse notification history, manage plugins and sinks, and tweak settings — all from a browser.

---

## Features

- **Plugin system** — load notification sources from external `.dll` assemblies at runtime; each plugin runs in isolation and can't crash the daemon
- **Sink system** — fan-out to any number of destinations: desktop popups, email, push notifications, webhooks, and more
- **Web UI** — React dashboard for history, plugins, sinks, logs, and config
- **Notification history** — optional SQLite-backed history with full-text search, source filter, and pagination (encrypted at rest)
- **D-Bus delivery** — notifications appear as native desktop popups via `org.freedesktop.Notifications`
- **REST API** — HTTP endpoints to send notifications from any language and manage plugins/sinks
- **Deduplication** — configurable time window to suppress duplicate notifications
- **NuGet installer** — install community plugins directly from NuGet.org via the API or web UI
- **systemd user service** — runs as a user unit, logs to the journal
- **Self-contained binary** — no .NET runtime required on the target machine

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                     Arrr.Service                     │
│                                                      │
│  Source Plugins ──┐                                  │
│  REST /api/notify ┼──▶  EventBus ──▶  SinkOrchestrator ──▶ D-Bus popup
│                   │                        │         │ ──▶ Ntfy / SMTP / Pushover
│                   │                        │         │ ──▶ Webhook / WebSocket
│                   │              HistoryService       │ ──▶ SignalR hub
│                                                      │ ──▶ … (sink plugins)
└──────────────────────────────────────────────────────┘
                          │
                    Web UI :5150
```

1. The daemon starts and loads plugins from the configured `plugins/` directory.
2. Each plugin receives an `IPluginContext` (event bus, logger, per-plugin config) and runs on its own task.
3. Plugins publish `Notification` events onto the internal event bus.
4. `SinkOrchestrator` fans every notification out to all enabled sinks in parallel.
5. If `historyEnabled: true`, every notification is also persisted to an encrypted SQLite database.
6. External processes can inject notifications via `POST /api/notify`.

---

## Getting Started

### Install from AUR (Arch Linux)

```bash
yay -S arrr-bin
# or
paru -S arrr-bin
```

### Install from package

Download the latest `.deb`, `.rpm`, or `.pkg.tar.zst` from the [releases page](https://github.com/tgiachi/Arrr/releases).

**Debian / Ubuntu**
```bash
sudo dpkg -i arrr_<version>_amd64.deb
```

**Fedora / RHEL**
```bash
sudo rpm -i arrr-<version>-1.x86_64.rpm
```

**Arch Linux (manual)**
```bash
sudo pacman -U arrr-<version>-1-x86_64.pkg.tar.zst
```

### Enable the systemd user service

```bash
systemctl --user enable --now arrr
journalctl --user -u arrr -f
```

The web UI is available at `http://localhost:5150` once the service is running.

### Build from source

```bash
git clone https://github.com/tgiachi/Arrr
cd Arrr
dotnet build -c Release
```

Run directly:
```bash
dotnet run --project src/Arrr.Service -- --rootDirectory ~/.local/share/arrr
```

---

## Configuration

On first run Arrr creates `$XDG_DATA_HOME/arrr/arrr.config` (defaults to `~/.local/share/arrr/arrr.config`).

```json
{
  "apiKey": "",
  "isDebug": false,
  "historyEnabled": false,
  "web": {
    "port": 5150
  },
  "deduplication": {
    "enabled": false,
    "windowSeconds": 300
  },
  "plugins": [],
  "sinks": []
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `apiKey` | `""` | API key for REST endpoints; leave empty to disable auth |
| `isDebug` | `false` | Enables OpenAPI (`/openapi/v1.json`) and Scalar UI (`/scalar/v1`) |
| `historyEnabled` | `false` | Persist all notifications to an encrypted SQLite database |
| `web.port` | `5150` | HTTP port for the REST API and web UI |
| `deduplication.enabled` | `false` | Suppress duplicate notifications within the time window |
| `deduplication.windowSeconds` | `300` | Window (seconds) for duplicate suppression |
| `plugins` | `[]` | List of enabled source plugins |
| `sinks` | `[]` | List of enabled sink plugins |

All settings can be changed from the web UI → **Config** tab without editing the JSON file directly.

---

## REST API

All endpoints require the `X-Api-Key` header when `apiKey` is set.

### Send a notification

```http
POST /api/notify
X-Api-Key: <your-key>
Content-Type: application/json

{
  "source": "my-script",
  "title": "Hello!",
  "body": "This is a notification from a shell script.",
  "iconUrl": null
}
```

### Notification history

```http
GET /api/history?page=1&limit=50&search=deploy&source=rss
X-Api-Key: <your-key>
```

Requires `historyEnabled: true`. Supports full-text search across title and body, source filter, and pagination.

### Plugins

```http
GET  /api/plugins                          # list loaded plugins
GET  /api/plugins/available                # all plugins in plugins/
POST /api/plugins/{id}/enable
POST /api/plugins/{id}/disable
POST /api/plugins/{id}/reload
POST /api/plugins/reload/all
POST /api/plugins/install                  # install from NuGet
POST /api/plugins/{id}/uninstall
GET  /api/plugins/{id}/config              # get plugin config JSON
PUT  /api/plugins/{id}/config              # save plugin config JSON
```

**Install from NuGet:**
```http
POST /api/plugins/install
Content-Type: application/json

{ "packageId": "Arrr.Plugin.Rss", "version": "1.0.0" }
```

Omit `version` to install the latest.

### Sinks

```http
GET  /api/sinks                            # list available sinks
POST /api/sinks/{id}/enable
POST /api/sinks/{id}/disable
POST /api/sinks/{id}/reload
GET  /api/sinks/{id}/config
PUT  /api/sinks/{id}/config
```

### Config backup / restore

```http
GET  /api/config/backup                    # export all plugin configs as JSON
POST /api/config/restore                   # import previously exported JSON
```

---

## Available Source Plugins

| Plugin | NuGet | Description |
|--------|-------|-------------|
| **RSS / Atom** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Rss)](https://www.nuget.org/packages/Arrr.Plugin.Rss) | Polls RSS/Atom feeds and notifies on new items |
| **IMAP** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Imap)](https://www.nuget.org/packages/Arrr.Plugin.Imap) | Monitors an IMAP mailbox and notifies on new mail |
| **Telegram** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Telegram)](https://www.nuget.org/packages/Arrr.Plugin.Telegram) | Receives Telegram messages via MTProto (WTelegramClient) |
| **WhatsApp** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.WhatsApp)](https://www.nuget.org/packages/Arrr.Plugin.WhatsApp) | Receives WhatsApp messages via whatsmeow bridge (QR pairing in UI) |
| **GitHub** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Github)](https://www.nuget.org/packages/Arrr.Plugin.Github) | Polls GitHub notifications (mentions, reviews, CI, etc.) |
| **CalDAV** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.CalDav)](https://www.nuget.org/packages/Arrr.Plugin.CalDav) | Polls ICS calendars and notifies for upcoming events |
| **Healthcheck** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Healthcheck)](https://www.nuget.org/packages/Arrr.Plugin.Healthcheck) | Polls HTTP endpoints and notifies on up/down state changes |
| **MQTT** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Mqtt)](https://www.nuget.org/packages/Arrr.Plugin.Mqtt) | Subscribes to MQTT topics and emits a notification per message |
| **systemd Journal** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Systemd)](https://www.nuget.org/packages/Arrr.Plugin.Systemd) | Tails `journalctl` and publishes log events as notifications |

---

## Available Sinks

| Sink | NuGet | Description |
|------|-------|-------------|
| **D-Bus** | built-in | Native desktop popups via `org.freedesktop.Notifications` |
| **Ntfy** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.Ntfy)](https://www.nuget.org/packages/Arrr.Sink.Ntfy) | Push to a [ntfy](https://ntfy.sh) topic |
| **SMTP** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.Smtp)](https://www.nuget.org/packages/Arrr.Sink.Smtp) | Send notifications by email (single or digest mode) |
| **Gotify** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.Gotify)](https://www.nuget.org/packages/Arrr.Sink.Gotify) | Push to a self-hosted [Gotify](https://gotify.net) server |
| **Pushover** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.Pushover)](https://www.nuget.org/packages/Arrr.Sink.Pushover) | Push to iOS/Android via [Pushover](https://pushover.net) |
| **Bark** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.Bark)](https://www.nuget.org/packages/Arrr.Sink.Bark) | Push to iOS via the [Bark](https://bark.day.app) app |
| **Telegram Bot** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.Telegram)](https://www.nuget.org/packages/Arrr.Sink.Telegram) | Send notifications to a Telegram chat via Bot API |
| **Home Assistant** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.HomeAssistant)](https://www.nuget.org/packages/Arrr.Sink.HomeAssistant) | Call a HA `notify` service |
| **Webhook** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.Webhook)](https://www.nuget.org/packages/Arrr.Sink.Webhook) | POST notifications as JSON to any HTTP endpoint |
| **WebSocket** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.WebSocket)](https://www.nuget.org/packages/Arrr.Sink.WebSocket) | Broadcast JSON frames to connected WebSocket clients |
| **SignalR** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.SignalR)](https://www.nuget.org/packages/Arrr.Sink.SignalR) | Broadcast to SignalR clients via a hub |
| **macOS Notify** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Sink.MacNotify)](https://www.nuget.org/packages/Arrr.Sink.MacNotify) | Native macOS notifications via `osascript` |

---

## Writing a Plugin

Implement `ISourcePlugin` from `Arrr.Core`, drop the compiled `.dll` into the `plugins/` directory and restart the daemon (or use the API to hot-reload).

```csharp
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

public class MyPlugin : ISourcePlugin
{
    public string Id          => "com.example.myplugin";
    public string Name        => "My Plugin";
    public string Version     => "1.0.0";
    public string Author      => "Your Name";
    public string Description => "Fetches something and sends notifications";
    public string[] Categories => ["example"];
    public string Icon        => "";

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await context.EventBus.PublishAsync(
                new Notification(Guid.NewGuid(), Id, "Hello", "World", DateTimeOffset.UtcNow, null),
                ct
            );
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

**Optional interfaces** a plugin can add:

| Interface | Purpose |
|-----------|---------|
| `IPollingPlugin` | Declare a poll interval; the host calls `PollAsync` on schedule |
| `IConfigurablePlugin<T>` | Persist typed config via `context.LoadConfigAsync<T>()` |
| `ICallbackPlugin` | Receive HTTP callbacks at `POST /api/plugins/{id}/callback` |
| `IQrPlugin` | Surface a QR code in the web UI for first-time pairing flows |

Plugins in **any language** can also inject notifications over HTTP — no .NET required:

```bash
curl -X POST http://localhost:5150/api/notify \
  -H "X-Api-Key: your-key" \
  -H "Content-Type: application/json" \
  -d '{"source":"bash","title":"Deploy done","body":"v1.2.3 is live","iconUrl":null}'
```

### Plugin Template

```bash
dotnet new install Arrr.Templates
dotnet new arrr-plugin -n MyPlugin \
    --PluginId com.example.myplugin \
    --Author "Your Name" \
    --Interval "00:05:00"
```

---

## License

MIT — see [LICENSE](LICENSE).
