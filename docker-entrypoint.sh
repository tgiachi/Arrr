#!/bin/sh
set -e

DATA_DIR="${XDG_DATA_HOME:-/data}/arrr"
CONFIG_FILE="$DATA_DIR/arrr.config"

mkdir -p "$DATA_DIR"

if [ ! -f "$CONFIG_FILE" ]; then
    # Use ARRR_API_KEY env var or generate a random key
    if [ -n "${ARRR_API_KEY:-}" ]; then
        API_KEY="$ARRR_API_KEY"
    else
        API_KEY="$(cat /proc/sys/kernel/random/uuid 2>/dev/null \
            || tr -dc 'a-zA-Z0-9' < /dev/urandom | head -c 32)"
    fi

    echo "=========================================="
    echo "  Arrr — first start"
    echo "  API Key: $API_KEY"
    echo "  Set ARRR_API_KEY env var to use a fixed key."
    echo "  Mount /data to persist config and plugins."
    echo "=========================================="

    cat > "$CONFIG_FILE" <<EOF
{
  "apiKey": "$API_KEY",
  "isDebug": false,
  "web": { "port": 5150 },
  "historyEnabled": false,
  "plugins": [],
  "sinks": [
    { "id": "com.arrr.sink.dbus", "enabled": false },
    { "id": "com.arrr.sink.socket", "enabled": false }
  ],
  "digest": { "enabled": false, "schedule": [] },
  "routing": { "enabled": false, "rules": [] }
}
EOF
fi

exec /app/arrr "$@"
