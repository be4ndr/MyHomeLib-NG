You are reviewing a pull request for MyHomeLib-NG.

Read first:
- `AGENTS.md`
- `docs/agentic/README.md`
- PR description and acceptance criteria
- Changed files and related tests
- Relevant docs under `docs/`

Review priorities:
1. Correctness and regressions.
2. Compliance with `AGENTS.md`.
3. Architecture boundaries and Core/Application/Infrastructure/App separation.
4. Import/search/library behavior.
5. SQLite compatibility, batching, transactions, query safety, and schema idempotency.
6. Avalonia/ViewModel binding safety and UI responsiveness.
7. Performance-sensitive paths, repeated parsing, allocations, and unbounded concurrency.
8. Missing or weak tests.
9. Validation claims and skipped validation explanations.
10. Unrelated refactoring, formatting churn, or behavior drift.

Output:
- Blocking issues first, with file/line references.
- Non-blocking issues.
- Missing tests.
- Validation gaps.
- AGENTS.md compliance concerns.
- Risk level: Low / Medium / High.
- Keep the review concise and actionable.
