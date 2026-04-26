# ImapPlugin

An [Arrr](https://github.com/tgiachi/Arrr) notification plugin.

## Build

```bash
dotnet build
```

## Deploy

Copy the compiled `bin/Release/net10.0/ImapPlugin.dll` into Arrr's `plugins/` directory,
then add the entry to `~/.local/share/arrr/arrr.config`:

```json
{
  "plugins": [
    { "id": "com.arrr.imap", "name": "ImapPlugin", "enabled": true }
  ]
}
```

Restart Arrr (`systemctl --user restart arrr`) — the plugin loads automatically.
