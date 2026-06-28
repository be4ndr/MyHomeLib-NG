# Agentic Development Workflow

This folder describes how AI-assisted contributors should work in MyHomeLib-NG. It is meant for Codex, Claude, ChatGPT, GitHub Copilot, and humans reviewing their output.

## Goals

- Keep AI changes small, reviewable, and testable.
- Protect offline catalog import, search behavior, SQLite compatibility, and UI responsiveness.
- Make agents inspect the repository before editing.
- Keep production code changes tied to explicit acceptance criteria.

## Repository Map

- `src/MyHomeLibNG.Core`: domain models, enums, interfaces, identity and normalization logic.
- `src/MyHomeLibNG.Application`: use-case services and orchestration over Core interfaces.
- `src/MyHomeLibNG.Infrastructure`: SQLite repositories, schema initialization, filesystem, ZIP/FB2/INPX, and providers.
- `src/MyHomeLibNG.App`: Avalonia windows, views, ViewModels, app composition, and platform actions.
- `tests/MyHomeLibNG.Tests`: Core, Application, and Infrastructure tests.
- `tests/MyHomeLibNG.App.Tests`: App and ViewModel tests.
- `tests/MyHomeLibNG.App.HeadlessTests`: Avalonia Headless binding and interaction tests.
- `.github/workflows`: CI and automated Codex PR review.
- `.codex/prompts`: reusable task prompts for local or hosted agents.

## Recommended Process

1. Read the issue, acceptance criteria, `AGENTS.md`, and any linked docs.
2. Search the repository for related types, tests, XAML bindings, SQL, and workflows.
3. Identify the affected layer before designing the change.
4. Choose the smallest implementation that preserves existing behavior outside the task.
5. Add or update tests in the matching test project.
6. Run targeted validation, then full validation when practical.
7. Report changed files, tests, validation commands, and residual risks.

## Agent Responsibilities

- Do not change production code for documentation-only tasks.
- Do not invent architecture or add dependencies without a clear need.
- Do not broadly refactor `MainWindowViewModel`; extract only one cohesive behavior at a time with tests.
- Preserve dependency direction: Core -> none, Application -> Core, Infrastructure -> Core/Application, App -> Application/Infrastructure.
- Keep SQLite schema changes idempotent and compatible with existing local databases.
- Keep import/search paths memory-bounded and cancellation-aware where practical.
- Keep Avalonia UI responsive and avoid direct SQLite access from UI code.

## Active Codex PR Review Workflow

The repository has an active advisory Codex review workflow at `.github/workflows/codex-review.yml`.

- It runs on pull requests when they are opened, synchronized, or reopened.
- It is skipped for fork PRs because forked pull requests cannot receive repository secrets.
- It checks out the pull request merge ref with repository credentials disabled.
- It validates that the repository OpenAI secret is available before running review.
- It runs `openai/codex-action@v1` with `.github/codex/prompts/review.md`.
- It uses `sandbox: read-only`, so review cannot edit files.
- It uploads `codex-review.md` as an artifact.
- It is advisory only. This workflow is not currently configured as a required merge gate.

The review prompt should stay aligned with `AGENTS.md` and should focus on architecture boundaries, SQLite/import/search risk, Avalonia/ViewModel binding risk, tests, validation claims, and performance-sensitive paths.

## Validation

Preferred full validation:

```bash
dotnet restore MyHomeLibNG.sln
dotnet build MyHomeLibNG.sln --configuration Release --no-restore
dotnet test MyHomeLibNG.sln --configuration Release --no-build
```

Targeted validation examples:

```bash
dotnet test tests/MyHomeLibNG.Tests/MyHomeLibNG.Tests.csproj --configuration Release
dotnet test tests/MyHomeLibNG.App.Tests/MyHomeLibNG.App.Tests.csproj --configuration Release
dotnet test tests/MyHomeLibNG.App.HeadlessTests/MyHomeLibNG.App.HeadlessTests.csproj --configuration Release
```

Always report what actually ran. If validation is skipped or fails because of environment setup, say so directly.
