# D-Bus Notify Integration — Design

**Date:** 2026-04-23
**Status:** Approved

---

## Goal

Inviare le notifiche Arrr al notification daemon di sistema Linux via `org.freedesktop.Notifications` (D-Bus), in parallelo al canale Unix socket già esistente. Le notifiche appaiono come popup nativi desktop (GNOME, KDE, ecc.).

---

## Architettura

Nessuna modifica a `Arrr.Core`. Tutto vive in `Arrr.Service`, seguendo il pattern già in uso con `SocketBroadcastSubscriber`.

```
Arrr.Service/
├── DBus/
│   └── INotifications.cs          ← interfaccia Tmds.DBus
└── Subscribers/
    ├── SocketBroadcastSubscriber.cs   (esistente)
    └── DBusNotifySubscriber.cs        ← nuovo IHostedService
```

Flusso:
```
IEventBus → DBusNotifySubscriber → org.freedesktop.Notifications (session bus)
IEventBus → SocketBroadcastSubscriber → /tmp/arrr.sock   (esistente)
```

I due canali sono indipendenti e paralleli.

---

## Interfaccia D-Bus

```csharp
// Arrr.Service/DBus/INotifications.cs
[DBusInterface("org.freedesktop.Notifications")]
internal interface INotifications : IDBusObject
{
    Task<uint> NotifyAsync(
        string appName,
        uint replacesId,
        string appIcon,
        string summary,
        string body,
        string[] actions,
        IDictionary<string, object> hints,
        int expireTimeout);
}
```

---

## Mapping Notification → Notify

| Parametro D-Bus | Valore |
|---|---|
| `appName` | `notification.Source` |
| `replacesId` | `0` (sempre nuova) |
| `appIcon` | `notification.IconUrl ?? ""` |
| `summary` | `notification.Title` |
| `body` | `notification.Body` |
| `actions` | `[]` |
| `hints` | `{}` |
| `expireTimeout` | `-1` (default desktop) |

---

## DBusNotifySubscriber

`IHostedService` (connessione async nel `StartAsync`):

- **`StartAsync`**: apre `Connection(Address.Session)`, chiama `ConnectAsync()`. Se fallisce (session bus non disponibile — es. server headless) logga `Warning` e ritorna senza registrare la subscription. Il servizio continua normalmente senza D-Bus.
- Se connesso: ottiene il proxy `INotifications` e registra `eventBus.Subscribe<Notification>(...)`.
- Per ogni `Notification`: chiama `NotifyAsync`. Se fallisce, logga `Error` e continua.
- **`StopAsync`**: chiude la connessione D-Bus.

---

## Modifiche a file esistenti

| File | Modifica |
|---|---|
| `Arrr.Service/Arrr.Service.csproj` | Aggiunge `<PackageReference Include="Tmds.DBus" Version="0.21.2" />` |
| `Arrr.Service/Program.cs` | Aggiunge `builder.Services.AddHostedService<DBusNotifySubscriber>()` |

---

## File da creare

| File | Azione |
|---|---|
| `Arrr.Service/DBus/INotifications.cs` | nuovo |
| `Arrr.Service/Subscribers/DBusNotifySubscriber.cs` | nuovo |

---

## Canali di comunicazione

| Canale | Consumer | Formato |
|---|---|---|
| Unix socket `/tmp/arrr.sock` | Tray Avalonia, client custom | JSON newline-delimited |
| D-Bus `org.freedesktop.Notifications` | Desktop system (GNOME/KDE) | chiamata D-Bus nativa |
