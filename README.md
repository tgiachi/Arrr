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
- **gRPC streaming** — server-streaming endpoint so remote clients (e.g. a PC) can subscribe to live notifications over the network
- **Routing rules** — filter or redirect notifications by source, title, body, priority, extras, and time-of-day; first-match-wins, disabled by default
- **Do Not Disturb** — pause all sink delivery with a single toggle (REST or gRPC), without stopping sources
- **Digest** — schedule batched notification summaries (hourly, daily, …) delivered via any sink
- **Deduplication** — configurable time window to suppress duplicate notifications
- **NuGet installer** — install community plugins directly from NuGet.org via the API or web UI
- **Docker image** — `tgiachi/arrr` on Docker Hub; first-start API key generation, `/data` volume for persistence
- **systemd user service** — runs as a user unit, logs to the journal
- **Self-contained binary** — no .NET runtime required on the target machine

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                        Arrr.Service                          │
│                                                              │
│  Source Plugins ──┐                                          │
│  REST /api/notify ┼──▶  EventBus ──▶  SinkOrchestrator ──▶ D-Bus popup
│                   │         │              │                 │ ──▶ Ntfy / SMTP / Pushover
│                   │         │         RoutingRules           │ ──▶ Webhook / WebSocket
│                   │         │         DnD guard              │ ──▶ SignalR hub
│                   │         │              │                 │ ──▶ … (sink plugins)
│                   │         │         HistoryService         │
│                   │         │                                │
│                   │         └──▶  NotificationGrpcService   │
│                   │                    │                     │
│                                  gRPC stream :5150           │
└──────────────────────────────────────────────────────────────┘
                          │
                    Web UI :5150
```

1. The daemon starts and loads plugins from the configured `plugins/` directory.
2. Each plugin receives an `IPluginContext` (event bus, logger, shared HTTP client, per-plugin config) and runs on its own task.
3. Plugins publish `Notification` events onto the internal event bus.
4. `SinkOrchestrator` evaluates routing rules and the DND flag, then fans matching notifications out to enabled sinks.
5. If `historyEnabled: true`, every notification is persisted to an encrypted SQLite database.
6. `NotificationGrpcService` streams events (notifications and DND changes) to all connected gRPC clients.
7. External processes can inject notifications via `POST /api/notify`.

---

## Getting Started

### Install from AUR (Arch Linux)

```bash
# Pre-built binary (fast):
yay -S arrr-bin

# Build from source (latest git HEAD):
yay -S arrr-git
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

### Docker

```bash
docker run -d \
  --name arrr \
  -p 5150:5150 \
  -v arrr-data:/data \
  tgiachi/arrr:latest
```

On first start Arrr prints a randomly generated API key to the log. Pass `ARRR_API_KEY` to use a fixed key:

```bash
docker run -d \
  --name arrr \
  -p 5150:5150 \
  -v arrr-data:/data \
  -e ARRR_API_KEY=my-secret-key \
  tgiachi/arrr:latest
```

> D-Bus and Unix socket sinks are disabled in the Docker image. Use Ntfy, SMTP, Webhook, or gRPC clients to receive notifications from a containerised instance.

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
  "web": { "port": 5150 },
  "deduplication": { "enabled": false, "windowSeconds": 300 },
  "digest": { "enabled": false, "schedule": [] },
  "routing": { "enabled": false, "rules": [] },
  "plugins": [],
  "sinks": []
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `apiKey` | `""` | API key for REST and gRPC endpoints; leave empty to disable auth |
| `isDebug` | `false` | Enables OpenAPI (`/openapi/v1.json`) and Scalar UI (`/scalar/v1`) |
| `historyEnabled` | `false` | Persist all notifications to an encrypted SQLite database |
| `web.port` | `5150` | HTTP/gRPC port for the REST API and web UI |
| `deduplication.enabled` | `false` | Suppress duplicate notifications within the time window |
| `deduplication.windowSeconds` | `300` | Window (seconds) for duplicate suppression |
| `digest.enabled` | `false` | Enable scheduled digest delivery |
| `routing.enabled` | `false` | Enable the routing rules engine |
| `routing.rules` | `[]` | Ordered list of routing rules (see [Routing Rules](#routing-rules)) |
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
  "iconUrl": null,
  "priority": 0
}
```

`priority`: `0` = Normal, `1` = High, `2` = Critical.

### Notification history

```http
GET /api/history?page=1&limit=50&search=deploy&source=rss
X-Api-Key: <your-key>
```

Requires `historyEnabled: true`. Supports full-text search across title and body, source filter, and pagination.

### Do Not Disturb

```http
GET /api/dnd
PUT /api/dnd
Content-Type: application/json

{ "enabled": true }
```

When DND is enabled, notifications are still collected and stored in history but not dispatched to any sink. DND state changes are also streamed to gRPC subscribers.

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

## gRPC

Arrr exposes a gRPC server-streaming endpoint on the **same port** as the REST API (HTTP/1.1 and HTTP/2 share port 5150). This lets a remote client — e.g. a desktop app on another machine — subscribe to live events without polling.

### Proto

```protobuf
service NotificationService {
  // Subscribe to live events (notifications + DND changes)
  rpc Subscribe (SubscribeRequest) returns (stream ArrEvent);
  // Toggle DND remotely
  rpc SetDnd    (SetDndRequest)   returns (DndResponse);
  rpc GetDnd    (GetDndRequest)   returns (DndResponse);
}
```

The proto file is at `src/Arrr.Service/Protos/notifications.proto`.

`ArrEvent` is a `oneof` that carries either a `NotificationEvent` or a `DndEvent`, so a single persistent stream receives both.

`SubscribeRequest.sources` is an optional list of source IDs to filter by — empty means all sources.

Authentication uses the `x-api-key` gRPC metadata header.

### Usage with grpcurl

```bash
# Subscribe to all events
grpcurl -plaintext \
  -proto src/Arrr.Service/Protos/notifications.proto \
  -H 'x-api-key: <key>' \
  localhost:5150 notifications.NotificationService/Subscribe

# Subscribe to a specific plugin only
grpcurl -plaintext \
  -proto src/Arrr.Service/Protos/notifications.proto \
  -H 'x-api-key: <key>' \
  -d '{"sources": ["com.arrr.plugin.todoist"]}' \
  localhost:5150 notifications.NotificationService/Subscribe

# Enable DND remotely
grpcurl -plaintext \
  -proto src/Arrr.Service/Protos/notifications.proto \
  -H 'x-api-key: <key>' \
  -d '{"enabled": true}' \
  localhost:5150 notifications.NotificationService/SetDnd
```

---

## Routing Rules

Routing rules let you filter or redirect notifications before they reach sinks. Rules are evaluated in order; the first match wins. Routing is **disabled by default** — enable it with `routing.enabled: true`.

```json
{
  "routing": {
    "enabled": true,
    "rules": [
      {
        "name": "Block low-priority RSS at night",
        "enabled": true,
        "sourcePattern": "com.arrr.plugin.rss",
        "minPriority": 0,
        "activeFrom": "22:00",
        "activeTo": "08:00",
        "block": true
      },
      {
        "name": "Critical alerts → SMTP only",
        "enabled": true,
        "minPriority": 2,
        "allowSinks": ["com.arrr.sink.smtp"]
      }
    ]
  }
}
```

| Field | Description |
|-------|-------------|
| `sourcePattern` | Exact source ID or trailing wildcard (`com.arrr.plugin.*`). Empty = any source. |
| `titleContains` | Case-insensitive substring match on the notification title. |
| `bodyContains` | Case-insensitive substring match on the notification body. |
| `minPriority` | `0` = Normal, `1` = High, `2` = Critical. Matches notifications at or above this level. |
| `extraConditions` | Additional key/value checks against `Notification.Extras`. |
| `activeFrom` / `activeTo` | Local time range (`HH:mm`, 24-hour). Supports midnight crossing. Empty = always active. |
| `block` | If `true`, the notification is dropped entirely. |
| `allowSinks` | Restrict delivery to these sink IDs. Empty = all running sinks. Ignored when `block: true`. |

All conditions on a rule are AND-ed. Rules are managed from the web UI → **Config** tab.

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
| **Todoist** | [![NuGet](https://img.shields.io/nuget/v/Arrr.Plugin.Todoist)](https://www.nuget.org/packages/Arrr.Plugin.Todoist) | Polls Todoist tasks and fires alerts for due dates and reminders |

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
            // Use the shared HttpClient — no socket exhaustion, no manual disposal
            var response = await context.Http.GetStringAsync("https://example.com/feed", ct);

            await context.EventBus.PublishAsync(
                new Notification(Guid.NewGuid(), Id, "Hello", "World", DateTimeOffset.UtcNow, null),
                ct
            );
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

`IPluginContext` provides:

| Member | Description |
|--------|-------------|
| `EventBus` | Publish notifications and subscribe to internal events |
| `Http` | Shared `HttpClient` backed by a pooled `SocketsHttpHandler`; one instance per plugin, safe to use directly |
| `Logger` | Scoped Serilog logger |
| `LoadConfigAsync<T>()` | Load (and hot-reload) typed per-plugin config |
| `SaveConfigAsync<T>()` | Persist typed per-plugin config |

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
