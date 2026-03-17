# HomeLib-Next

A modern, high-performance desktop library manager inspired by MyHomeLib. https://github.com/OleksiyPenkov/MyHomeLib

## Freeware app description

MyHomeLibNext is planned as a **freeware desktop application** distributed through GitHub releases.
It is designed for readers, librarians, and collectors who need an offline-first tool that remains responsive even with very large catalogs.

### Planned freeware distribution model

- Free to download and use.
- Open source repository with public issue tracking.
- Cross-platform desktop binaries published in GitHub Releases.
- Simple upgrade path: download a newer release and replace the previous installation.

## Stack choices

- **C# / .NET 8** for a modern LTS runtime.
- **Avalonia UI** for a native desktop-first, cross-platform UI.
- **SQLite** for fast local metadata storage in v1.
- **Modular monolith architecture** to keep v1 simple while allowing future scaling.

## Solution structure

```text
MyHomeLibNext.sln
src/
  MyHomeLibNext.App/             # Avalonia desktop UI
  MyHomeLibNext.Core/            # domain models, enums, interfaces
  MyHomeLibNext.Application/     # application services / use cases
  MyHomeLibNext.Infrastructure/  # SQLite, filesystem-ready infrastructure implementations
tests/
  MyHomeLibNext.Tests/           # unit tests
```

### Dependency direction

- `MyHomeLibNext.App` -> `Application`, `Infrastructure`
- `MyHomeLibNext.Application` -> `Core`
- `MyHomeLibNext.Infrastructure` -> `Core`, `Application`
- `MyHomeLibNext.Core` -> (no project dependencies)

This keeps infrastructure and UI concerns separated from domain concerns:

- UI does not access SQLite directly.
- Core does not depend on Infrastructure.
- Application does not depend on Avalonia.

## Implemented in this step

- Domain models: `LibraryProfile`, `Book`, `BookSource`, `Author`.
- Enums: `LibraryType`, `SourceKind`, `FileFormat`.
- `ILibraryRepository` interface in Core.
- SQLite schema initializer in Infrastructure.
- `SqliteLibraryRepository` implementation using plain ADO.NET (`Microsoft.Data.Sqlite`).
- Basic DI wiring for Application + Infrastructure.
- Minimal Avalonia main window with placeholder library/books panels.
- Initial unit test for repository add/get flow.

## Next implementation steps

1. Add library switching and active-library context service.
2. Expand schema for books/authors/sources and indexes for large collections.
3. Add background import pipeline for local folders and archive scanning.
4. Introduce remote-library API adapters in Infrastructure.
5. Add view models and observable UI bindings for library and books lists.
6. Add packaging/update workflow for desktop distribution.
