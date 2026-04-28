# Routing Rules

Routing rules let you filter or redirect notifications before they reach sinks. Rules are evaluated in order; the first match wins. Routing is **disabled by default** — enable it with `routing.enabled: true` in `arrr.config` or from the web UI.

## Example

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

## Rule fields

| Field | Description |
|-------|-------------|
| `name` | Human-readable label (shown in the web UI) |
| `enabled` | If `false`, the rule is skipped entirely |
| `sourcePattern` | Exact source ID or trailing wildcard (`com.arrr.plugin.*`). Empty = any source. |
| `titleContains` | Case-insensitive substring match on the notification title. |
| `bodyContains` | Case-insensitive substring match on the notification body. |
| `minPriority` | `0` = Normal, `1` = High, `2` = Critical. Matches notifications at or above this level. |
| `extraConditions` | Additional key/value checks against `Notification.Extras`. |
| `activeFrom` / `activeTo` | Local time range (`HH:mm`, 24-hour). Supports midnight crossing. Empty = always active. |
| `block` | If `true`, the notification is dropped entirely and no sink receives it. |
| `allowSinks` | Restrict delivery to these sink IDs. Empty = all running sinks. Ignored when `block: true`. |

All conditions on a rule are AND-ed together. Rules are managed from the web UI → **Routing** tab without restarting the daemon.

## Behaviour

- If no rule matches, the notification is delivered to all running sinks (default-allow).
- `block: true` stops processing immediately — subsequent rules are not evaluated.
- `allowSinks` does not block other sinks at the routing level; sinks that are not listed simply do not receive this notification.
- DND takes precedence over routing — if DND is enabled, no sink receives anything regardless of rules.
