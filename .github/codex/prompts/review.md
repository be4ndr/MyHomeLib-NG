You are reviewing a pull request for MyHomeLib-NG.

Read `AGENTS.md`, the PR description, changed files, and related tests before reviewing.

Project stack:
- C# / .NET 10
- Avalonia UI
- SQLite
- Modular monolith

Review priorities:
1. Correctness
2. Regressions in search/import/library behavior
3. UI responsiveness
4. SQLite performance and transaction safety
5. Unnecessary allocations or repeated parsing
6. Unrelated refactoring
7. Missing tests
8. Broken dependency direction

Architecture rules:
- App may depend on Application and Infrastructure.
- Application may depend on Core.
- Infrastructure may depend on Core and Application.
- Core must not depend on App, Infrastructure, or Avalonia.
- UI must not access SQLite directly.

Check whether:
- Acceptance criteria are covered.
- Tests were added or updated.
- Async code uses cancellation where practical.
- Long-running operations do not block the UI thread.
- SQLite writes are batched where relevant.
- SQLite schema and repository changes preserve existing local databases where practical.
- No local-only sample data was committed.
- The PR avoids unrelated refactoring, formatting churn, and behavior drift.

Output:
- Summary
- Blocking issues
- Non-blocking issues
- Missing tests
- Risk level: Low / Medium / High
