# MyHomeLib-NG

A modern, high-performance desktop library manager inspired by MyHomeLib. https://github.com/OleksiyPenkov/MyHomeLib

## Freeware app description

MyHomeLibNG is planned as a **freeware desktop application** distributed through GitHub releases.
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
MyHomeLibNG.sln
src/
  MyHomeLibNG.App/             # Avalonia desktop UI
  MyHomeLibNG.Core/            # domain models, enums, interfaces
  MyHomeLibNG.Application/     # application services / use cases
  MyHomeLibNG.Infrastructure/  # SQLite, filesystem-ready infrastructure implementations
tests/
  MyHomeLibNG.Tests/           # unit tests
```

### Dependency direction

- `MyHomeLibNG.App` -> `Application`, `Infrastructure`
- `MyHomeLibNG.Application` -> `Core`
- `MyHomeLibNG.Infrastructure` -> `Core`, `Application`
- `MyHomeLibNG.Core` -> (no project dependencies)

This keeps infrastructure and UI concerns separated from domain concerns:

- UI does not access SQLite directly.
- Core does not depend on Infrastructure.
- Application does not depend on Avalonia.

## Offline ZIP/FB2 import

Local folder libraries use:

- an `.inpx` catalog for source configuration and fallback catalog loading
- ZIP archives with `.fb2` entries for the indexed local book store
- SQLite as the searchable local index

### Current import behavior

- ZIP archives are scanned as streams; archives are not extracted to disk.
- FB2 entries are parsed directly from archive entry streams; entries are not buffered as whole archives in RAM.
- Import runs as a bounded pipeline:
  - one archive/entry producer
  - a small FB2 parser worker pool
  - one SQLite writer
- SQLite writes are batched inside transactions to avoid per-book commits.
- Duplicate rows are prevented by `(LibraryProfileId, ArchivePath, EntryPath)`.
- Unchanged books are skipped by content hash.
- Malformed FB2 files are recorded as failures and do not stop the import.

### Indexed metadata

Initial import stores enough metadata for usable search and display:

- title
- authors
- series
- series number
- genres
- language
- publish year when present in FB2
- archive path
- archive entry path
- file name
- file size

Annotation and cover extraction are skipped during bulk import to keep throughput high.

Cover image decoding and thumbnail generation are intentionally deferred during bulk import so the first pass stays fast and memory-bounded.

### Search and UI behavior

- Once a library has indexed rows in SQLite, offline search and directory browsing use the indexed SQLite data.
- This means the Main window and search results reflect imported metadata instead of depending on raw INPX fields.
- Title, author, series, genres, language, year filters, and keyword search operate on the populated indexed fields.
- Keyword search is tokenized (multiple words are matched as multiple terms).
- Book content still opens directly from the ZIP archive entry.

### INPX compatibility notes

- Generic `structure.info`-based INPX files are supported.
- Legacy Flibusta-style INPX archives without `structure.info` are also supported when the `.inp` files describe archive entries in the classic format.
- For legacy Flibusta catalogs, the `.inp` file name is mapped to the matching `.zip` archive name.

### Current limitations

- Bulk import does not generate cover thumbnails or annotation text during the initial pass.
- If a library has not been imported yet, offline search falls back to the INPX catalog view.
- Search uses regular SQLite indexed fields, not FTS.
