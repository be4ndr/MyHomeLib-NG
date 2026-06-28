You are reviewing a pull request for MyHomeLib-NG.

Read before reviewing:
- `AGENTS.md`
- `docs/agentic/README.md`
- The PR description and acceptance criteria
- Changed files, related source files, and related tests
- Any validation output or claims in the PR

Project stack:
- C# / .NET 10
- Avalonia UI
- SQLite
- Modular monolith

Review priorities:
1. Correctness
2. Compliance with `AGENTS.md`
3. Broken architecture boundaries
4. Regressions in import/search/library behavior
5. SQLite compatibility, performance, and transaction safety
6. Avalonia/ViewModel binding and responsiveness risks
7. Performance-sensitive paths, unnecessary allocations, repeated parsing, and unbounded concurrency
8. Missing or weak tests
9. Unrelated refactoring, formatting churn, or behavior drift

Architecture rules:
- Core owns domain models, enums, interfaces, normalization, and matching.
- Core must not depend on App, Infrastructure, Avalonia, SQLite, filesystem, HTTP, or DI.
- Application owns use cases and orchestration over Core abstractions.
- Application may depend on Core but must not depend on Avalonia or concrete SQLite/UI details.
- Infrastructure owns SQLite, filesystem, ZIP/FB2/INPX parsing, providers, and storage.
- Infrastructure may depend on Core and Application.
- App owns Avalonia views, windows, ViewModels, composition, and platform actions.
- App may depend on Application and Infrastructure.
- UI must not access SQLite directly.

Check whether:
- Acceptance criteria are covered.
- Tests were added or updated.
- Test placement matches the changed layer.
- Validation claims are specific and believable; do not accept unsubstantiated "tests pass" claims.
- Skipped validation is explained.
- Public APIs and production behavior are preserved unless explicitly required.
- Async code uses cancellation where practical.
- Long-running operations do not block the UI thread.
- Import paths remain streaming, batched, and memory-bounded.
- Search semantics remain compatible with the current indexed-field approach unless explicitly changed.
- SQLite writes are batched and transactional where relevant.
- SQLite schema initialization remains idempotent.
- SQLite schema and repository changes preserve existing local databases where practical.
- SQL uses parameters for data values.
- Avalonia XAML bindings match ViewModel properties.
- ViewModel property changes raise notifications when behavior depends on them.
- UI, ViewModel, dialog, visibility, or binding changes have appropriate ViewModel or Avalonia Headless tests.
- `MainWindowViewModel` was not broadly refactored or behavior-changed as cleanup.
- No local-only sample data was committed.
- The PR avoids unrelated refactoring, formatting churn, and behavior drift.

Output:
- Summary
- Blocking issues with file/line references
- Non-blocking issues
- Missing or weak tests
- Validation concerns
- AGENTS.md compliance concerns
- Risk level: Low / Medium / High
- Keep the review concise and actionable.
