---
name: Codex task
about: Task designed for Codex implementation
title: "[Codex] "
labels: codex, needs-review
---

## Goal

Describe the desired user-visible behavior or technical result.

## Current behavior

What happens now?

## Desired behavior

What should happen after this task?

## Scope

### In scope
- 

### Out of scope
- 

## Affected area

- [ ] Core
- [ ] Application
- [ ] Infrastructure / SQLite
- [ ] App / ViewModels
- [ ] Avalonia UI
- [ ] Tests
- [ ] Documentation

## Acceptance criteria

- [ ] 
- [ ] 
- [ ] 

## Required tests

### Unit tests
- [ ] 

### Integration tests
- [ ] 

### UI / ViewModel tests
- [ ] 

### Manual validation
- [ ] 

## Validation commands

```bash
dotnet restore
dotnet build MyHomeLibNG.sln --configuration Release
dotnet test MyHomeLibNG.sln --configuration Release --no-build
```

## Codex instructions
- Keep the change focused.
- Do not refactor unrelated code.
- Add or update tests for changed behavior.
- Explain any skipped tests.
- Summarize changed files and validation results.