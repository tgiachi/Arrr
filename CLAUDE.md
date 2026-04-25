# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project

Arrr is a Linux desktop notification aggregator daemon. It loads notification-source plugins at runtime, routes all notifications through an in-process event bus, and delivers them to desktop (D-Bus), Unix socket, or any configured sink plugin.

---

## Commands

### .NET

```bash
# Build everything
dotnet build -c Release

# Run all non-integration tests
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj -c Release \
  --filter "Category!=Integration" \
  --logger "console;verbosity=normal"

# Run a single test class
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj \
  --filter "ClassName=Arrr.Tests.Service.ConfigBackupServiceTests"

# Run a single test method
dotnet test tests/Arrr.Tests/Arrr.Tests.csproj \
  --filter "Name=ExportAsync_ReadsAllConfigFiles"
```

### Local NuGet feed — run after ANY change to Arrr.Core

```bash
bash scripts/pack-dev.sh
```

Plugins and the test project resolve `Arrr.*` packages exclusively from `./local-packages/` (see `nuget.config`). If you add a new public type to `Arrr.Core` and skip this step, plugin builds and tests will fail with `TypeLoadException` or `NU1301`.

### UI

```bash
cd ui
npm install
npm run dev      # dev server proxying localhost:5150
npm run build    # TypeScript check + Vite build → ../src/Arrr.Service/wwwroot/
```

---

## Architecture

### Data flow

```
Plugins (ISourcePlugin) ──┐
REST POST /api/notify ────┼──▶ IEventBus ──▶ SinkOrchestrator ──▶ DbusNotifySink
                          └──▶             └──▶ UnixSocketSink
                                           └──▶ Sink plugins (WebSocket, SMTP, etc.)
```

### Arrr.Core (NuGet package)

Shared library consumed by plugins as a `PackageReference`. Contains:
- **`Interfaces/`** — all contracts (`ISourcePlugin`, `ISinkPlugin`, `IEventBus`, `IPluginContext`, `ISinkContext`, `IPollingPlugin`, `IConfigurablePlugin`, `ICallbackPlugin`, `IQrPlugin`, `IConfigBackupService`, …)
- **`Data/Notifications/`** — `Notification` record (the single payload type)
- **`Data/Config/`** — `ArrrConfig`, `PluginEntry`, `SinkEntry`
- **`Utils/VersionUtils`** — `VersionUtils.Get(typeof(MyPlugin))` → version from assembly

### Arrr.Service

Main daemon. Key hosted services:

| Class | Role |
|---|---|
| `PluginOrchestrator` | Scans `plugins/`, loads each DLL into a collectible `AssemblyLoadContext`, starts/stops plugins, handles hot-reload via FileSystemWatcher |
| `SinkOrchestrator` | Manages all sinks (built-in + plugin), subscribes each to `IEventBus`, routes notifications |
| `EventBusService` | In-process pub/sub; `PublishAsync<T>` / `Subscribe<T>` |
| `ConfigService` | Loads/saves `arrr.config`; fields marked `[Sensitive]` are AES-encrypted at rest |
| `PluginContextFactory` | Creates `IPluginContext` instances injected at plugin `StartAsync` |
| `NuGetPluginInstaller` | Downloads plugins from NuGet.org into `plugins/` |

REST endpoints are minimal-API extensions mapped in `Program.cs`:
- `MapExternalApi()` → `POST /api/notify`
- `MapPluginsApi()` → `/api/plugins/*`
- `MapSinksApi()` → `/api/sinks/*`
- `MapConfigBackupApi()` → `GET /api/config/backup`, `POST /api/config/restore`

All endpoints authenticate via `X-Api-Key` header using `ApiAuth.TryAuthenticate`.

### Plugins

A plugin is a `.dll` that implements `ISourcePlugin` (or `ISinkPlugin` for output connectors). Minimal example:

```csharp
public class MyPlugin : ISourcePlugin
{
    public string Id      => "com.example.myplugin";
    public string Name    => "My Plugin";
    public string Author  => "you (you@example.com)";
    public string Version => VersionUtils.Get(typeof(MyPlugin));

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        await context.EventBus.PublishAsync(
            new Notification(Guid.NewGuid(), Id, "Title", "Body", DateTimeOffset.UtcNow, null), ct);
    }
}
```

Optional interfaces a plugin can add: `IPollingPlugin`, `IConfigurablePlugin<T>`, `ICallbackPlugin`, `IQrPlugin`.

### Config files

Runtime data lives under `$XDG_DATA_HOME/arrr/` (default `~/.local/share/arrr/`):

| Path | Contents |
|---|---|
| `arrr.config` | Main config (port, API key, plugin list) |
| `configs/<pluginId>.config` | Per-plugin JSON config, sensitive fields encrypted |
| `plugins/` | Installed plugin assemblies |
| `logs/` | Serilog rolling log files |

`IConfigBackupService` exports/imports everything under `configs/` as a single JSON dict.

---

## Commit rules

- All commit messages (title and body) must be in **English**.
- Follow **Conventional Commits**: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`.
- For commits with multiple changes, list every fix/feat in the body.
- **Never** add `Co-Authored-By` lines.
- Do not commit files under `docs/superpowers/` or `docs/plans/`.

---

## Custom commands

- `/vai` — Read all modified (or untracked) files, then create a conventional commit without asking for confirmation. Include all changes in the body.
- `/comment` — Add `///` XML doc comments to all interfaces found in the current scope.
- `/release` — See the [Release workflow](#cicd) section and `AGENTS.md` for the full procedure.

---

## Code conventions

These rules are checked in code review; violations will be flagged:

- **One type per file**, file name matches type name.
- **Namespace = folder path**: `src/Arrr.Core/Utils/Foo.cs` → `namespace Arrr.Core.Utils;`
- **Class layout**: constants → private readonly fields (`_camelCase`) → properties → constructor → public methods → private methods → Dispose (always last).
- **No primary constructors**, no expression-bodied constructors. Always `{ }` body.
- **No `string.Empty`** — use `""`.
- Enums go in the `Types` namespace with a domain prefix (`DirectoryType`, not just `Type`).
- DTOs go in `Data/`; internal types go in a `Data.Internal` sub-namespace.
- Interfaces go in `Interfaces/` and get `///` XML doc comments.
- Logging: `private readonly ILogger _logger = Log.ForContext<T>();` (Serilog, no constructor injection).
- Async methods end with `Async` and accept `CancellationToken ct`.

### Test conventions

```
tests/Arrr.Tests/<Domain>/<Subdomain>/<SubjectName>Tests.cs
namespace Arrr.Tests.<Domain>.<Subdomain>;
```

Test method name pattern: `Method_Scenario_ExpectedResult`.
Shared fakes/helpers live in `tests/Arrr.Tests/Support/`.
Integration tests that require D-Bus are marked `[Category("Integration"), Explicit(...)]` and excluded from CI.

---

## CI/CD

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | PR to `main` or `develop` | Packs Core → restore → build → test (no Integration) |
| `nuget-publish.yml` | Push tag `v*` | Packs + publishes all NuGet packages with tag version |
| `release.yml` | Merge to `main` | If PR had `release` label → semantic-release → GitHub release + .deb/.rpm/.pkg.tar.zst |

### /release — how to ship a new version

1. Open a PR from `develop` to `main`.
2. Add the `release` label to the PR **before merging**.
3. Merge the PR.
4. `release.yml` runs automatically: semantic-release bumps the version, creates the tag, builds packages, and publishes the GitHub Release.
5. The new tag triggers `nuget-publish.yml`, which publishes all packages to NuGet.org.

> Do **not** push version tags manually — let semantic-release manage them.
> If a manual tag was already pushed, semantic-release will pick the next patch (e.g., `v1.5.0` → creates `v1.5.1`).
