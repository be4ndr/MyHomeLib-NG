# Codex Project Instructions

## Project Context

MyHomeLib-NG is a freeware, offline-first desktop library manager inspired by the original MyHomeLib project.

Stack:

* C# / .NET 8
* Avalonia UI
* SQLite
* Modular monolith
* xUnit tests
* GitHub Issues / Pull Requests

Main goals:

* Keep the desktop UI responsive.
* Support large offline catalogs.
* Keep import/search fast and memory-bounded.
* Preserve simple architecture.
* Make changes small, testable, and reviewable.

---

## Solution Structure

```text
MyHomeLibNG.sln
src/
  MyHomeLibNG.App/             # Avalonia desktop UI and ViewModels
  MyHomeLibNG.Core/            # domain models, enums, interfaces
  MyHomeLibNG.Application/     # application services and use cases
  MyHomeLibNG.Infrastructure/  # SQLite, filesystem, ZIP/FB2/INPX, providers
tests/
  MyHomeLibNG.Tests/           # current unit/integration tests
```

Dependency rules:

* `Core` must not depend on App, Infrastructure, Avalonia, SQLite, filesystem, or HTTP.
* `Application` may depend on Core.
* `Infrastructure` may depend on Core and Application.
* `App` may depend on Application and Infrastructure.
* UI must not access SQLite directly.
* Application must not depend on Avalonia.

---

## General Rules

* Keep changes focused on the requested task.
* Prefer incremental improvements over large rewrites.
* Do not refactor unrelated modules.
* Do not introduce speculative architecture.
* Do not add dependencies unless clearly needed.
* Do not redesign UI unless explicitly requested.
* Preserve existing behavior unless the task asks to change it.
* Prefer readable, maintainable code over clever code.
* Do not claim tests passed unless they were actually run.

---

## Codex Workflow

For each task:

1. Read the issue/request.
2. Identify affected area: Core, Application, Infrastructure, App/ViewModels, UI, Tests, Docs.
3. Define or infer narrow acceptance criteria.
4. Add or update tests when behavior changes.
5. Make the smallest safe implementation.
6. Run targeted tests first.
7. Run broader validation near the end when practical.
8. Summarize changed files, tests, validation, and risks.

If requirements are unclear, make the smallest safe assumption and mention it in the final response.

---

## Current Product Behavior

Preserve these behaviors unless explicitly asked to change them:

* Local library import is INPX-first.
* ZIP/FB2 parsing is fallback and future enrichment support.
* SQLite stores indexed metadata for search and display.
* Search and directory browsing use imported SQLite metadata when available.
* Keyword search supports multiple terms.
* Book content opens from ZIP archive entries.
* Cover decoding and thumbnail generation are deferred during bulk import.
* Bulk import must remain fast and memory-bounded.
* Search currently uses regular SQLite indexed fields, not FTS.

---

## Local Validation Data

A local-only validation folder may exist:

```text
example/
```

It may contain sample archives, databases, configs, and offline catalogs.

Rules:

* Use it for local validation when available.
* Do not assume it exists in CI.
* Do not commit it.
* Do not write tests that require it.
* Do not hardcode local absolute paths.

---

## Test Rules

Use tests according to the changed area.

### Core

Add unit tests for:

* domain behavior
* normalization
* parsing
* matching
* edge cases

Core tests must not use Avalonia, SQLite, filesystem, or network.

### Application

Add unit tests with fake repositories/providers for:

* use cases
* service orchestration
* success paths
* failure paths
* cancellation behavior when relevant

### Infrastructure

Add integration or fixture-based tests for:

* SQLite schema/repository behavior
* INPX parsing
* ZIP/FB2 fallback behavior
* imports
* search persistence
* duplicate archive/entry paths
* missing or malformed metadata

Use temporary SQLite databases for repository tests.

### App / ViewModels

Add ViewModel tests for:

* state transitions
* commands/actions
* selected library/book behavior
* busy/error/status behavior
* derived properties
* `PropertyChanged` when behavior depends on it

### Avalonia UI

Use Avalonia Headless tests when changing:

* bindings
* visibility rules
* control interaction
* window/dialog behavior
* important UI state transitions

Do not use UI tests for pure ViewModel logic.

---

## MainWindowViewModel Rules

`MainWindowViewModel` is large and behavior-sensitive.

Do not broadly refactor it.

Do not:

* rewrite it in one pass
* rename bound properties without updating XAML and tests
* change search behavior as cleanup
* change directory browsing behavior as cleanup
* mix UI redesign with ViewModel extraction

Allowed safe extraction pattern:

1. Add or identify tests for current behavior.
2. Extract one small cohesive behavior.
3. Keep public ViewModel behavior unchanged.
4. Add/update tests.
5. Run targeted validation.

Preferred extraction candidates:

* search request building
* structured book matching
* directory browser state building
* directory alphabet grouping
* source health building
* book launch request building

Extract only one area per task.

---

## Performance Rules

Optimize for:

* low memory usage
* low allocation pressure
* efficient SQLite access
* batched writes
* bounded concurrency
* responsive UI

Avoid:

* loading large files fully into memory
* repeated parsing
* repeated normalization
* repeated database queries
* unbounded `Task.WhenAll`
* per-record commits in hot paths
* blocking the UI thread
* excessive UI refreshes

Use streaming, batching, transactions, and cancellation tokens where practical.

---

## SQLite Rules

* Prefer batched writes.
* Use transactions for bulk operations.
* Avoid unnecessary roundtrips.
* Prefer UPSERT where practical.
* Keep schema initialization idempotent.
* Keep migration/backfill bounded.
* Preserve compatibility with existing local databases where practical.

When changing schema:

* update schema initializer
* update repository mapping
* add tests for empty database creation
* add tests for existing database behavior when practical

---

## UI Rules

* Keep UI responsive.
* Do not block the UI thread.
* Do not redesign layout unless requested.
* Do not change user-facing text unless needed.
* Do not rename bound ViewModel properties casually.
* Keep empty, loading, and error states clear.
* Avoid mixing UI layout changes with backend behavior changes.

---

## Documentation Rules

Update README or docs when application behavior changes.

Add useful comments for:

* non-obvious parsing logic
* performance-sensitive logic
* batching
* concurrency
* migration/backfill behavior
* important tradeoffs

Avoid comments that merely repeat the code.

---

## Validation Commands

Preferred full validation:

```bash
dotnet restore MyHomeLibNG.sln
dotnet build MyHomeLibNG.sln --configuration Release
dotnet test MyHomeLibNG.sln --configuration Release --no-build
```

Preferred targeted validation:

```bash
dotnet test tests/MyHomeLibNG.Tests/MyHomeLibNG.Tests.csproj --configuration Release
```

Run targeted validation during implementation.

Run full validation near the end when practical.

If validation cannot be run, clearly say why.

---

## Final Response Format

At the end of each task, report:

```text
Summary:
- ...

Modified files:
- ...

Tests:
- ...

Validation:
- ...

Acceptance criteria:
- ...

Risks / notes:
- ...
```

Do not invent validation results.

---

## Hard Guardrails

Do not:

* refactor unrelated modules
* introduce unnecessary dependencies
* introduce speculative architecture
* switch core technologies without approval
* redesign UI without request
* broadly refactor MainWindowViewModel
* commit local validation data
* add network-dependent tests to the default test suite
* change search/import semantics without acceptance criteria
* hide failed tests or known issues
* claim performance improvements without reasoning or evidence
