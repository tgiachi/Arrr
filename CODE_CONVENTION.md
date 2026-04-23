# Code Convention

This document defines coding conventions for Moongate. It is intentionally strict to keep the codebase consistent and readable.

## 1. General Principles

- Prefer clarity over cleverness.
- Keep domain boundaries explicit.
- Keep files small and focused.
- Avoid hidden magic and implicit behavior.
- Write code that is easy to reason about under load and during debugging.

## 2. Project Structure and Namespaces

### 2.1 Folder-to-Namespace Rule

- Namespace must match folder path.
- Example: `src/Moongate.Server/Services/Spatial/SpatialWorldService.cs` -> `namespace Moongate.Server.Services.Spatial;`

### 2.2 Domain-First Organization

- Group by domain first, not by technical suffix.
- Use existing project domains (`Services/*`, `Handlers`, `FileLoaders`, `Data/*`, `Interfaces/*`, `Types/*`, `Extensions/*`).

### 2.3 Mandatory Namespace Buckets

- `Types`: enums and type constants domain-wide.
- `Data`: DTOs and simple data carriers.
- `Data.Internal`: internal-only data models/implementation details.
- `Interfaces`: contracts only.

## 3. C# File and Type Rules

- One `.cs` file must contain at most one primary type (`class`, `record`, or `enum`).
- File name must match type name.
- Use file-scoped namespaces.
- Keep `Dispose` methods as the last methods in the class.

## 4. Class Layout Order

Inside a type, use this order:

1. `const` fields
2. `private readonly` fields (must start with `_`)
3. non-readonly fields
4. properties
5. constructor(s)
6. public methods
7. protected methods
8. private methods
9. `Dispose`/finalization methods (last)

### 4.1 Private Readonly Naming

- All `private readonly` fields must start with `_`.
- Examples:
  - `_logger`
  - `_gameEventBusService`
  - `_persistenceService`

### 4.2 Dispose Position

- If a class implements `IDisposable` and/or `IAsyncDisposable`, `Dispose`/`DisposeAsync` must be the last method(s) in the class.

## 5. Interfaces

- Interfaces live only under `Interfaces` namespaces.
- Every interface must have XML docs (`///`).
- Interface names must use `I` prefix and clear domain naming.

## 6. Enums

- Enums must live under a `Types` namespace for their domain.
- Prefer explicit underlying type when protocol/storage needs fixed size.

## 7. Serialization and Persistence

- Persisted entities must be explicit and version-safe.
- When adding persistent fields, update:
  - entity model
  - MemoryPack annotations or ignore rules
  - tests
- Never rely on runtime-only fields for persistence correctness.

## 8. Event and Runtime State Rules

- Runtime source of truth is session/runtime state, not stale persistence reads.
- Events should carry enough context to avoid extra ambiguous lookups.
- Cross-thread communication must go through queues/bus abstractions.

## 9. Test Conventions

### 9.1 Structure

- Tests must be organized by domain/component, mirroring production code.
- Avoid placing test files directly in project root test folders.
- Use dedicated folders such as:
  - `Server/Handlers`
  - `Server/Services/<Domain>`
  - `Server/FileLoaders`
  - `Server/Bootstrap`
  - `Server/Data/Events/<Domain>`

### 9.2 Naming

- File: `<SubjectName>Tests.cs`
- Class: `<SubjectName>Tests`
- One main test class per file.

### 9.3 Namespace Rule (Tests)

- Namespace must match test path under `tests/Moongate.Tests`.
- Example:
  - `tests/Moongate.Tests/Server/Services/Events/GameEventBusServiceTests.cs`
  - `namespace Moongate.Tests.Server.Services.Events;`

### 9.4 Test Support

- Shared fakes/builders/helpers go in `Support` or `TestSupport`.
- Do not mix reusable test infrastructure into domain test files.

## 10. Source Generator and Runtime Safety

- Prefer static, explicit registrations over runtime reflection when possible.
- Keep public API and generated code deterministic.
- New reflection-heavy code must be justified and tested against the affected runtime paths.

## 11. Commits

- Use Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, etc.).
- Keep commits scoped and meaningful.

## 12. Non-Negotiable Hygiene

- No dead code.
- No TODO comments without a tracked follow-up.
- No inconsistent naming across domains.
- Keep warnings under control; do not normalize noisy warnings.

## 13. Additional Conventions

1. Nullability
- Use nullable reference types consistently.
- Avoid null-forgiving (`!`) unless explicitly justified in code.

2. Async naming and signatures
- Async methods must end with `Async`.
- Include `CancellationToken` on I/O-bound public async methods.

3. Logging
- Use static message templates.
- Keep template shape stable across calls.
- Do not use string interpolation for structured logs.

4. Exception handling
- Use guard clauses (`ArgumentNullException.ThrowIfNull`, etc.).
- Do not swallow exceptions silently.

5. API collection exposure
- Expose read-only collection contracts (`IReadOnlyList<>`, `IReadOnlyDictionary<>`) where mutation is not intended.

6. Hot-path allocations
- Minimize allocations in network/game-loop hot paths.
- Prefer value-oriented and span-based APIs when appropriate.

7. Persistence mapping completeness
- Any persisted field change must include MemoryPack contract updates and test coverage.

8. Test naming style
- Prefer `Method_Scenario_ExpectedResult`.
- Keep tests focused on a single behavior.

9. No magic numbers
- Replace protocol/timing literals with named constants.

10. Using directives
- Keep usings ordered and deterministic (system first, then project/local).
