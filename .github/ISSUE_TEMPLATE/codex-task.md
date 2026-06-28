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
dotnet restore MyHomeLibNG.sln
dotnet build MyHomeLibNG.sln --configuration Release --no-restore
dotnet test MyHomeLibNG.sln --configuration Release --no-build
```

## Codex instructions
- Read AGENTS.md first.
- Explore the current repository before coding.
- Keep the change focused.
- Do not refactor unrelated code.
- Preserve existing behavior outside the acceptance criteria.
- Add or update tests for changed behavior.
- Explain any skipped tests.
- Summarize changed files and validation results.
