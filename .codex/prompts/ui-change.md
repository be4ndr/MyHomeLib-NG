You are implementing an Avalonia UI or ViewModel change in MyHomeLib-NG.

Before coding:
- Read `AGENTS.md`.
- Inspect related XAML, code-behind, ViewModels, app tests, and headless tests.
- Identify bound property names before renaming or moving anything.
- Confirm whether the task is UI-only, ViewModel-only, or both.

Rules:
- Do not redesign layout unless explicitly requested.
- Keep UI responsive and do not block the UI thread.
- Keep SQLite and filesystem access out of UI code.
- Test ViewModel logic without Avalonia when possible.
- Use Avalonia Headless tests for bindings, visibility, dialog behavior, and important interactions.

Validation:
- Run affected App/ViewModel tests.
- Run headless tests when XAML or UI interaction changes.
- Run full validation when practical.

Final response:
- Summary
- Modified files
- Tests
- Validation
- UI risks / notes

