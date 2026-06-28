You are fixing a bug in MyHomeLib-NG.

Before coding:
- Read `AGENTS.md`.
- Reproduce or trace the issue from existing tests, source code, logs, or the issue description.
- Search for related behavior and regression tests before changing code.
- Identify the smallest layer where the bug should be fixed.

Implementation rules:
- Add a failing test or expand an existing test when practical.
- Fix the root cause without changing unrelated behavior.
- Preserve SQLite/database/catalog compatibility.
- Keep UI fixes responsive and avoid blocking the UI thread.
- Do not use local `example/` data in committed tests.

Validation:
- Run the bug-focused test first.
- Run related targeted tests.
- Run full validation when practical.

Final response:
- Root cause
- Summary
- Modified files
- Tests
- Validation
- Risks / notes

