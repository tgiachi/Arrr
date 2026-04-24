# Arrr! ☠️

<p align="center">
  <img src="assets/arr_logo.png" alt="Arrr! logo" width="128"/>
</p>

> *Arrr!* — because every good notification deserves a pirate's welcome.

Arrr is a Linux desktop notification aggregator daemon. It runs as a background service, collects notifications from multiple sources via a plugin system, and delivers them to your desktop through D-Bus (the standard freedesktop.org Notifications API) and a Unix domain socket for any client that wants to listen.

---

## Features

- **Plugin system** — load notification sources from external `.dll` assemblies at runtime; each plugin runs in isolation and can't crash the daemon
- **D-Bus delivery** — notifications appear as native desktop popups via `org.freedesktop.Notifications`
- **Unix socket broadcast** — newline-delimited JSON stream on `/tmp/arrr.sock` for custom clients and scripts
- **REST API** — HTTP endpoints to send notifications from any language and inspect loaded plugins
- **systemd user service** — runs as a user unit, logs to the journal
- **Self-contained binary** — no .NET runtime required on the target machine

---

## Architecture

```
┌─────────────────────────────────────────────────┐
│                  Arrr.Service                    │
│                                                  │
│  Plugin A ──┐                                    │
│  Plugin B ──┼──▶  EventBus  ──▶  DBusNotify     │
│  Plugin C ──┘          │                         │
│  REST /api/notify ──────┘         SocketServer   │
│                                        │         │
└────────────────────────────────────────┼─────────┘
                                         │
                              /tmp/arrr.sock  (JSON stream)
```

1. The daemon starts and loads plugins from the configured `plugins/` directory.
2. Each plugin receives an `IPluginContext` (event bus, logger, HTTP callback URL) and runs on its own task.
3. Plugins publish `Notification` events onto the internal event bus.
4. Two subscribers consume every notification: one forwards it to D-Bus (desktop popup), the other broadcasts it as JSON over the Unix socket.
5. External processes (plugins written in any language, scripts, etc.) can also inject notifications via `POST /api/notify`.

---

## Getting Started

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

**Arch Linux**
```bash
sudo pacman -U arrr-<version>-1-x86_64.pkg.tar.zst
```

### Enable the systemd user service

```bash
systemctl --user enable --now arrr
journalctl --user -u arrr -f
```

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

On first run Arrr creates `$XDG_DATA_HOME/arrr/arrr.config` (defaults to `~/.local/share/arrr/arrr.config`):

```json
{
  "socketPath": "/tmp/arrr.sock",
  "apiKey": "",
  "isDebug": false,
  "web": {
    "port": 5150
  },
  "plugins": []
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `socketPath` | `/tmp/arrr.sock` | Unix socket path |
| `apiKey` | `""` | API key for REST endpoints (leave empty to disable) |
| `isDebug` | `false` | Enables OpenAPI (`/openapi/v1.json`) and Scalar UI (`/scalar/v1`) |
| `web.port` | `5150` | HTTP port for the REST API |
| `plugins` | `[]` | List of enabled plugins |

---

## REST API

All endpoints require the `X-Api-Key` header (set `apiKey` in config first).

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

### List loaded plugins

```http
GET /api/plugins
X-Api-Key: <your-key>
```

### List available plugins (all DLLs in plugins/)

```http
GET /api/plugins/available
X-Api-Key: <your-key>
```

Returns each plugin with `enabled` and `running` fields.

### Enable / Disable a plugin

```http
POST /api/plugins/{pluginId}/enable
POST /api/plugins/{pluginId}/disable
X-Api-Key: <your-key>
```

### Reload plugins

```http
POST /api/plugins/{pluginId}/reload    # reload single plugin
POST /api/plugins/reload/all           # reload all plugins
X-Api-Key: <your-key>
```

### Install a plugin from NuGet

Plugins published on NuGet.org with the `arrr-plugin` tag can be installed directly:

```http
POST /api/plugins/install
X-Api-Key: <your-key>
Content-Type: application/json

{ "packageId": "Arrr.Plugin.Rss", "version": "1.0.0" }
```

Omit `version` to install the latest. The installer downloads the package and its dependencies, extracts the DLLs into the `plugins/` directory and starts the plugin automatically.

### Uninstall a plugin

```http
POST /api/plugins/Arrr.Plugin.Rss/uninstall
X-Api-Key: <your-key>
```

---

## Available Plugins

| Plugin | ID | Description | Auth |
|--------|----|-------------|------|
| **RSS / Atom** | `com.arrr.rss` | Polls one or more RSS/Atom feeds and notifies on new items | None |
| **IMAP** | `com.arrr.imap` | Monitors an IMAP mailbox via IDLE and notifies on new e-mails | Username / password |
| **Telegram** | `com.arrr.telegram` | Receives messages on your Telegram user account via MTProto (WTelegramClient). Verification code delivered via `POST /api/plugins/{id}/callback` | Phone + QR/code |
| **WhatsApp** | `com.arrr.whatsapp` | Receives WhatsApp messages via a Go bridge (whatsmeow). First-time QR pairing shown directly in the web UI | QR scan (in-UI) |

### Building the WhatsApp bridge

The WhatsApp plugin requires a small compiled Go binary (requires Go ≥ 1.22 and GCC):

```bash
cd plugins/WhatsAppPlugin/bridge
./build.sh          # fetches deps, compiles → ./whatsapp-bridge
```

Set `BridgePath` in the plugin config to the absolute path of the produced binary.

---

## Plugin Template

Install the `dotnet new` template to scaffold a new plugin in seconds:

```bash
dotnet new install Arrr.Templates
dotnet new arrr-plugin -n MyRssPlugin \
    --PluginId com.example.rss \
    --Author "Your Name" \
    --Interval "00:05:00"
```

This generates a ready-to-build project with `IPollingPlugin` pre-wired and all metadata filled in.

---

## Writing a Plugin

Implement `ISourcePlugin` from `Arrr.Core`, drop the compiled `.dll` into the `plugins/` directory and restart the daemon.

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

Plugins written in **any language** can also send notifications over HTTP — no .NET required:

```bash
curl -X POST http://localhost:5150/api/notify \
  -H "X-Api-Key: your-key" \
  -H "Content-Type: application/json" \
  -d '{"source":"bash","title":"Deploy done","body":"v1.2.3 is live","iconUrl":null}'
```

---

## Listening on the Unix Socket

Every notification is broadcast as a single JSON line:

```bash
socat - UNIX-CONNECT:/tmp/arrr.sock
```

```json
{"id":"...","source":"rss","title":"New post","body":"...","timestamp":"2026-04-24T12:00:00+00:00","iconUrl":null}
```

---

## License

MIT — see [LICENSE](LICENSE).
