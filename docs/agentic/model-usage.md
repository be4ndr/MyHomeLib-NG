# AI Model Usage Guide

Use the smallest capable model for the job, and reserve stronger reasoning for changes with more risk.

## Good Uses

- Fast coding assistants: local edits, search, boilerplate, test scaffolding, and small docs updates.
- Strong reasoning models: architecture-sensitive changes, import/search behavior, SQLite schema changes, concurrency, and PR review.
- UI-capable models: Avalonia XAML changes, binding checks, empty/loading/error states, and headless test planning.
- Review models: independent checks for regression risk, missing tests, dependency direction, and performance concerns.

## Automated PR Review

`.github/workflows/codex-review.yml` runs the active Codex PR review for same-repository pull requests. It uses the repository secret configured for the OpenAI API key, keeps the Codex sandbox read-only, skips fork PRs, and uploads the review as an artifact.

Treat this review as advisory. It should help humans find architecture, SQLite/import/search, Avalonia/ViewModel, testing, validation, and performance risks, but it is not a required merge gate.

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
