You are implementing a feature in MyHomeLib-NG.

Before coding:
- Read `AGENTS.md`.
- Inspect the current repository structure, related source files, tests, docs, and GitHub workflows.
- Identify the affected layer: Core, Application, Infrastructure, App/ViewModels, Avalonia UI, Tests, or Docs.
- Define narrow acceptance criteria and out-of-scope behavior.

Implementation rules:
- Keep the change small and focused.
- Preserve existing behavior outside the requested feature.
- Keep dependency direction intact.
- Add or update tests in the matching test project.
- Avoid broad refactors and new dependencies unless clearly required.

Validation:
- Run targeted tests for the changed area.
- Run full validation when practical:
  - `dotnet restore MyHomeLibNG.sln`
  - `dotnet build MyHomeLibNG.sln --configuration Release --no-restore`
  - `dotnet test MyHomeLibNG.sln --configuration Release --no-build`

Final response:
- Summary
- Modified files
- Tests
- Validation
- Acceptance criteria
- Risks / notes

