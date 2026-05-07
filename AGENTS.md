# Codex Project Instructions

## General Principles

- Prefer incremental improvements over large rewrites.
- Keep changes focused on the requested task.
- Avoid modifying unrelated modules.
- Prefer maintainable and readable solutions over clever solutions.
- Optimize for long-term maintainability.
- Keep architecture simple unless complexity is clearly justified.

---

## Model and Cost Efficiency

- Default model: GPT-5.3-Codex
- Default reasoning level: Medium
- Be cost-conscious and efficient.
- Avoid repeated expensive operations.
- Avoid repeated full builds and full test runs.
- Prefer targeted inspection and targeted tests.
- Before expensive actions, explain why they are needed.
- Run full validation only near the end of the task.

---

## Local Validation Data

A local-only validation dataset may exist on the developer machine in:

example/

This folder is NOT committed to git.

It may contain:
- sample archives
- sample databases
- sample configuration files
- offline datasets

Use local example data for validation when available.

Do not:
- assume the folder exists in CI
- commit local datasets to git
- modify local validation files unless explicitly requested

---

## Refactoring Rules

When refactoring:
- remove dead code
- remove unused variables
- remove unnecessary allocations
- remove redundant loops
- simplify complex logic
- reduce excessive nesting
- avoid duplicated code

If the same logic appears more than twice:
- extract a reusable helper method or service

If multiple functions operate on the same concept:
- group them into a dedicated class or service

Do not introduce abstractions unless they improve:
- readability
- maintainability
- reuse
- performance

Avoid architecture for architecture’s sake.

---

## Performance Rules

Optimize for:
- CPU usage
- memory usage
- allocation reduction
- I/O efficiency
- batching
- bounded concurrency

Avoid:
- loading large files fully into memory
- unnecessary temporary collections
- repeated parsing
- repeated normalization
- repeated database queries
- unbounded concurrency
- excessive UI refresh/update frequency

Use streaming and batching where practical.

---

## Database Rules

- Prefer batched writes.
- Prefer transactions for bulk operations.
- Avoid per-record commits in hot paths.
- Avoid unnecessary database roundtrips.
- Prefer UPSERT patterns over existence-check-then-update patterns where practical.
- Avoid unnecessary updates when data has not changed.

---

## Concurrency Rules

- Keep concurrency bounded and configurable.
- Avoid unbounded `Task.WhenAll`.
- Use cancellation tokens in long-running operations.
- Avoid locks in hot paths unless necessary.
- Prefer channels/queues with bounded capacity for pipelines.
- Prevent uncontrolled memory growth.

---

## Documentation Rules

- Add XML documentation comments to new public/internal classes.
- Add XML documentation comments to new public/internal methods.
- Comment non-obvious private logic only.
- Avoid useless comments.
- Explain:
    - performance-sensitive behavior
    - concurrency behavior
    - batching logic
    - important tradeoffs

Update README when application behavior changes.

---

## Validation Rules

After meaningful changes:
1. Run targeted tests for the changed area.
2. Run lightweight validation where possible.
3. Verify behavior still works correctly.
4. Verify no obvious regressions were introduced.

Near the end:
- run broader validation if necessary
- summarize results clearly

Do not repeatedly run expensive validation loops.

---

## UI Rules

- Keep UI responsive.
- Avoid blocking the UI thread.
- Avoid unnecessary refreshes or rerenders.
- Do not redesign UI unless explicitly requested.

---

## Pull Request Output

At the end of each task provide:
1. Summary of changes
2. Modified files
3. Validation/tests performed
4. Remaining risks or bottlenecks
5. Suggested next improvements

---

## Hard Guardrails

Do not:
- refactor unrelated modules
- introduce speculative architecture
- introduce unnecessary dependencies
- switch core technologies without explicit approval
- hide failed validation or known issues
- claim optimizations without reasoning or evidence