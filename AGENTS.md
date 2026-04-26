# AGENTS.md

Shared instructions for all AI agents (Claude Code, Codex, Copilot, etc.) working in this repository.

---

## Custom commands

### /release

Ship a new version to NuGet and GitHub Releases.

**Correct workflow:**

1. Ensure all changes are committed and pushed to `develop`.
2. Open a PR from `develop` to `main`:
   ```bash
   gh pr create --base main --head develop --title "..." --body "..."
   ```
3. Add the `release` label to the PR **before merging**:
   ```bash
   gh pr edit <number> --add-label "release"
   ```
4. Merge the PR (or ask the user to merge it).
5. `release.yml` runs automatically on the `main` push:
   - Detects the `release` label on the merged PR.
   - Runs semantic-release → determines next version from conventional commits.
   - Creates tag `vX.Y.Z`, writes `CHANGELOG.md`, builds `.deb`/`.rpm`/`.pkg.tar.zst`.
   - Publishes the GitHub Release with package artifacts.
6. The new tag triggers `nuget-publish.yml` → publishes all NuGet packages to NuGet.org.

**Do NOT** push version tags manually. If a manual tag already exists for the computed version, semantic-release will increment to the next patch automatically.

---

## Commit rules

- Messages must be in **English** (title and body).
- Follow **Conventional Commits**: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`.
- Multi-change commits: list every fix/feat in the body, one per line.
- **Never** add `Co-Authored-By: Claude` or any AI attribution.

---

## Code conventions (summary)

Full details are in `CODE_CONVENTION.md`. Key rules:

- One type per file; file name = type name.
- Namespace mirrors folder path (`src/Arrr.Core/Utils/Foo.cs` → `namespace Arrr.Core.Utils;`).
- No primary constructors, no expression-bodied constructors.
- Use `""` not `string.Empty`.
- Enums → `Types/` namespace with domain prefix.
- Interfaces → `Interfaces/` with XML doc comments.
- Logging: `Log.ForContext<T>()` (Serilog), no constructor injection.
- Tests: `tests/Arrr.Tests/<Domain>/<SubjectName>Tests.cs`, method pattern `Method_Scenario_ExpectedResult`.

---

## Local NuGet feed

After any change to `Arrr.Core`, run before building plugins or tests:

```bash
bash scripts/pack-dev.sh
```

Plugins resolve `Arrr.*` only from `./local-packages/` (enforced by `nuget.config`). Skipping this step causes `NU1301` or `TypeLoadException`.
