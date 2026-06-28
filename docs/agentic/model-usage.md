# AI Model Usage Guide

Use the smallest capable model for the job, and reserve stronger reasoning for changes with more risk.

## Good Uses

- Fast coding assistants: local edits, search, boilerplate, test scaffolding, and small docs updates.
- Strong reasoning models: architecture-sensitive changes, import/search behavior, SQLite schema changes, concurrency, and PR review.
- UI-capable models: Avalonia XAML changes, binding checks, empty/loading/error states, and headless test planning.
- Review models: independent checks for regression risk, missing tests, dependency direction, and performance concerns.

## Suggested Pairing

- Feature implementation: one coding model to implement, one review model to inspect.
- Bug fix: one model reproduces or traces the issue, then implements the narrow fix.
- Refactor: one model maps dependencies and tests first; keep the refactor small.
- UI change: one model edits XAML/ViewModels, then validates bindings with headless tests.
- Infrastructure change: use a stronger reasoning model for SQLite, import, parsing, batching, and compatibility.

## Prompting Rules

- Ask the model to inspect existing code before writing code.
- Include affected area, acceptance criteria, out-of-scope items, and required validation.
- Ask for exact test commands and exact results.
- Tell the model not to change unrelated production behavior.
- For reviews, ask for blocking issues first, then non-blocking issues and missing tests.

## When To Be Careful

- SQLite schema and repository mapping changes.
- Import/search semantics and directory browsing behavior.
- `MainWindowViewModel` changes.
- Long-running import, parsing, network, or UI operations.
- Changes that could affect existing user databases or local catalogs.

