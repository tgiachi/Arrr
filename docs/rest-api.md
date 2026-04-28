# REST API

All endpoints require the `X-Api-Key` header when `apiKey` is set in the config.

## Send a notification

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

Any language or tool that can make HTTP requests can inject notifications — no .NET required:

```bash
curl -X POST http://localhost:5150/api/notify \
  -H "X-Api-Key: your-key" \
  -H "Content-Type: application/json" \
  -d '{"source":"bash","title":"Deploy done","body":"v1.2.3 is live"}'
```

## Notification history

```http
GET /api/history?page=1&limit=50&search=deploy&source=rss
X-Api-Key: <your-key>
```

Requires `historyEnabled: true` in the config. Supports full-text search across title and body, source filter, and pagination.

## Do Not Disturb

```http
GET /api/dnd
PUT /api/dnd
Content-Type: application/json

{ "enabled": true }
```

When DND is enabled, notifications are still collected and stored in history but not dispatched to any sink. DND state changes are pushed in real-time to all connected SignalR clients (e.g. `arrr-tray`).

## Plugins

```http
GET  /api/plugins                    # list loaded plugins
GET  /api/plugins/available          # all plugins in plugins/
POST /api/plugins/{id}/enable
POST /api/plugins/{id}/disable
POST /api/plugins/{id}/reload
POST /api/plugins/reload/all
POST /api/plugins/install            # install from NuGet
POST /api/plugins/{id}/uninstall
GET  /api/plugins/{id}/config        # get plugin config JSON
PUT  /api/plugins/{id}/config        # save plugin config JSON
GET  /api/plugins/{id}/icon          # get plugin icon (PNG)
```

**Install from NuGet:**

```http
POST /api/plugins/install
Content-Type: application/json

{ "packageId": "Arrr.Plugin.Rss", "version": "1.0.0" }
```

Omit `version` to install the latest available version.

## Sinks

```http
GET  /api/sinks                      # list available sinks
POST /api/sinks/{id}/enable
POST /api/sinks/{id}/disable
POST /api/sinks/{id}/reload
GET  /api/sinks/{id}/config
PUT  /api/sinks/{id}/config
GET  /api/sinks/{id}/icon            # get sink icon (PNG)
```

## Icons bundle

```http
GET /api/icons
X-Api-Key: <your-key>
```

Returns all plugin and sink icons as base64-encoded PNG strings in a single response — useful for clients that want to cache all icons on connect:

```json
{
  "plugins": { "com.arrr.rss": "<base64>", ... },
  "sinks":   { "com.arrr.sink.dbus": "<base64>", ... }
}
```

## Config backup / restore

```http
GET  /api/config/backup              # export all plugin configs as JSON
POST /api/config/restore             # import previously exported JSON
```

## Version

```http
GET /api/version
```

```json
{ "version": "1.7.0", "runtimeVersion": ".NET 10.0", "isDebug": false }
```
