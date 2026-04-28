# Configuration

On first run Arrr creates `$XDG_DATA_HOME/arrr/arrr.config` (defaults to `~/.local/share/arrr/arrr.config`).

All settings can be changed from the web UI → **Config** tab without editing the JSON file directly.

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
| `apiKey` | `""` | API key for all endpoints; leave empty to disable auth |
| `isDebug` | `false` | Enables OpenAPI (`/openapi/v1.json`) and Scalar UI (`/scalar/v1`) |
| `historyEnabled` | `false` | Persist all notifications to an encrypted SQLite database |
| `web.port` | `5150` | Port for the REST API, web UI, and SignalR hub |
| `deduplication.enabled` | `false` | Suppress duplicate notifications within the time window |
| `deduplication.windowSeconds` | `300` | Window (seconds) for duplicate suppression |
| `digest.enabled` | `false` | Enable scheduled digest delivery |
| `routing.enabled` | `false` | Enable the routing rules engine |
| `routing.rules` | `[]` | Ordered list of routing rules (see [routing.md](routing.md)) |
| `plugins` | `[]` | List of enabled source plugins |
| `sinks` | `[]` | List of enabled sink plugins |

## Data directory layout

```
~/.local/share/arrr/
├── arrr.config               # main config
├── history.db                # notification history (SQLite, encrypted)
├── configs/
│   └── <pluginId>.config     # per-plugin JSON config (sensitive fields AES-encrypted)
├── plugins/                  # installed plugin DLLs
└── logs/
    └── log-YYYYMMDD.txt      # rolling log files
```

The data directory can be changed with `--rootDirectory /custom/path` at startup.
