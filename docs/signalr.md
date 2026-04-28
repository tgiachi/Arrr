# SignalR streaming

Arrr exposes a SignalR hub at `/stream` on the same port as the REST API (default **5150**). This lets any client subscribe to live events — notifications and DND changes — without polling.

`arrr-tray` uses this hub to receive desktop notifications in real time.

## Authentication

Pass the API key as a query parameter:

```
ws://localhost:5150/stream?key=<your-api-key>
```

## Messages (server → client)

| Method | Payload | Description |
|--------|---------|-------------|
| `ReceiveNotification` | `Notification` object | A new notification was published |
| `DndChanged` | `bool enabled` | DND state was toggled |

`Notification` shape:

```json
{
  "id": "uuid",
  "source": "com.arrr.rss",
  "title": "New item",
  "body": "...",
  "iconUrl": null,
  "priority": 0,
  "timestamp": "2025-01-01T12:00:00Z"
}
```

## JavaScript example

```js
import { HubConnectionBuilder } from '@microsoft/signalr'

const conn = new HubConnectionBuilder()
  .withUrl(`http://localhost:5150/stream?key=${apiKey}`)
  .withAutomaticReconnect()
  .build()

conn.on('ReceiveNotification', (notif) => {
  console.log(notif.title, notif.body)
})

conn.on('DndChanged', (enabled) => {
  console.log('DND:', enabled)
})

await conn.start()
```

## .NET example

```csharp
var hub = new HubConnectionBuilder()
    .WithUrl($"http://localhost:5150/stream?key={apiKey}")
    .WithAutomaticReconnect()
    .Build();

hub.On<TrayNotification>("ReceiveNotification", notif =>
    Console.WriteLine($"{notif.Title}: {notif.Body}"));

hub.On<bool>("DndChanged", enabled =>
    Console.WriteLine($"DND: {enabled}"));

await hub.StartAsync();
```
