You are implementing an Infrastructure change in MyHomeLib-NG.

Before coding:
- Read `AGENTS.md`.
- Inspect related repository, parser, provider, schema, import, and search tests.
- Identify compatibility expectations for existing databases, INPX files, ZIP archives, and FB2 metadata.

Rules:
- Keep hot paths streaming, batched, and memory-bounded.
- Use transactions for bulk SQLite writes.
- Keep schema initialization idempotent.
- Preserve search/import semantics unless the task explicitly changes them.
- Use parameterized SQL for data values.
- Add integration or fixture-based tests with temporary databases where relevant.
- Do not commit local `example/` validation data.

Validation:
- Run targeted Infrastructure tests.
- Run full validation when practical.

Final response:
- Summary
- Modified files
- Tests
- Validation
- Compatibility and performance notes

