# Contributing to DataImportExportManager

Thank you for contributing to **DataImportExportManager**. This document describes the development environment setup, coding standards, branch strategy, and pull request process that all contributors are expected to follow.

---

## Table of Contents

- [Development Environment](#development-environment)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [SOLID Principles](#solid-principles)
- [Branch Strategy](#branch-strategy)
- [Commit Message Format](#commit-message-format)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)
- [Definition of Done](#definition-of-done)

---

## Development Environment

| Tool | Version |
|---|---|
| .NET SDK | 10.0 |
| C# | 14.0 |
| IDE | Visual Studio 2026+ or VS Code with C# Dev Kit |

**Setup steps:**

```powershell
# Clone the repository
git clone https://github.com/TroyCrowe-TCCoder/DataImportExportManager
cd DataImportExportManager

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test
```

---

## Project Structure

```
DataImportExportManager/
├── Interfaces/                  # Public contracts
│   ├── IDataImporter.cs         # Stream -> tabular data
│   ├── IDataExporter.cs         # Tabular data -> stream
│   └── IDataFormatRouter.cs     # Deterministic format selection
├── Contracts/                   # Public data contracts
│   └── TabularImportResult.cs   # Columns + data rows import shape
├── Importers/                   # IDataImporter implementations
├── Exporters/                   # IDataExporter implementations
├── Services/                    # Routing/orchestration
├── Extensions/                  # DI registration
├── DataImportExportManager.csproj
├── README.md
├── CONTRIBUTING.md              # This file
├── CHANGELOG.md
└── scripts/
    └── validate.ps1

DataImportExportManager.Tests/   # xUnit test project
```

---

## Coding Standards

This project targets **.NET 10 / C# 14** with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

### Naming

- **Classes, Interfaces, Methods, Properties:** `PascalCase`
- **Private fields:** `_camelCase` (underscore prefix)
- **Parameters and locals:** `camelCase`
- **Async methods:** always suffix with `Async` (e.g., `ImportAsync`)
- **Interfaces:** prefix with `I` followed by `PascalCase` (e.g., `IDataImporter`)

### Code Style

- Use **file-scoped namespaces**: `namespace DataImportExportManager;`
- Prefer **implicit types** (`var`) when the type is obvious from the right-hand side
- Use **target-typed `new`** where the type is already stated
- Use **expression body** for single-line members
- Enable and respect **nullable reference types** — annotate all public API signatures
- Add **XML doc comments** (`///`) on all public types and members
- Keep methods short and focused — if a method exceeds ~20 lines, consider splitting it
- No magic strings — extract constants or use `nameof`

### Async

- All I/O methods must be `async Task` or `async Task<T>` — no synchronous wrappers
- Accept `CancellationToken` in every public async method signature and pass it through
- Use `ConfigureAwait(false)` in all library code
- Never use `.Result` or `.Wait()` — these cause deadlocks in async contexts

### Error Handling

- Use `ArgumentNullException.ThrowIfNull(param)` for null guards
- Use `string.IsNullOrWhiteSpace(s)` for string guards — throw `ArgumentException` with a helpful message
- Convert malformed caller input into precise public exceptions as early as possible
- Never catch and swallow exceptions silently
- Do not catch base `Exception` unless logging and rethrowing

### Dependencies

- Do not add new NuGet packages without prior discussion in the pull request
- Do not upgrade existing packages without a corresponding `CHANGELOG.md` entry

---

## SOLID Principles

All changes must adhere to SOLID principles. Reviewers will reject contributions that violate them.

| Principle | Expectation |
|---|---|
| **SRP** | Each class has exactly one reason to change. Importers parse; exporters write; the router selects. Keep them separate. |
| **OCP** | Add new formats by implementing `IDataImporter`/`IDataExporter` and registering them with the DI container — do not modify stable, tested code to add unrelated features. |
| **LSP** | All implementations must satisfy their interface contract fully. Do not implement a method to throw `NotImplementedException`. |
| **ISP** | Keep interfaces narrow. If a new interface would force consumers to depend on methods they don't use, split it. |
| **DIP** | All cross-component dependencies must be injected via constructor. No `new ConcreteType()` inside a class that could receive the dependency from outside. |

---

## Branch Strategy

This repository uses a **feature branch workflow** through `dev` and `main`.

| Branch type | Pattern | Purpose |
|---|---|---|
| Feature | `feature/<short-description>` | New functionality promoted through `dev` |
| Bug fix | `fix/<short-description>` | Corrects a defect |
| Refactor | `refactor/<short-description>` | No behaviour change |
| Documentation | `docs/<short-description>` | Docs only |
| Release | `release/<semver>` | Release preparation |

**Rules:**
- Branch off `dev` for normal work unless a maintainer directs otherwise
- Keep branches short-lived (ideally < 5 business days)
- Rebase onto `dev` before raising a PR — no merge commits in feature branches
- Delete branches after merge

---

## Commit Message Format

Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <short summary>

[optional body]

[optional footer(s)]
```

**Types:**

| Type | When to use |
|---|---|
| `feat` | New feature or capability |
| `fix` | Bug fix |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `test` | Adding or updating tests |
| `docs` | Documentation only |
| `chore` | Build, dependency, or tooling changes |
| `perf` | Performance improvement |

**Examples:**

```
feat(XmlImporter): add object-element row mode support

fix(DataFormatRouter): normalize .jsonl alias to .ndjson

docs(README): add branch governance flow section
```

---

## Pull Request Process

1. **Open a PR** against `dev` with a clear title using the Conventional Commits format
2. **Reference related issues** in the PR description (e.g. `Fixes #<id>`)
3. Fill in the PR template (below) completely
4. Ensure CI checks pass before requesting review
5. At least **one approving review** is required before merge
6. Approved `feature/* -> dev` PRs are completed after required checks succeed

### PR Description Template

```markdown
## Summary
<!-- What does this PR do? -->

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Refactor
- [ ] Documentation
- [ ] Dependency update

## Testing
<!-- How was this change verified? -->

## Checklist
- [ ] `CHANGELOG.md` updated
- [ ] Tests added/updated
- [ ] `pwsh ./scripts/validate.ps1 -Pack` passes locally
```

---

## Testing Requirements

- All new public behaviour must be covered by tests in `DataImportExportManager.Tests`
- Tests must be deterministic — no reliance on wall-clock time, network access, or file-system ordering
- Run `dotnet test` locally before opening a PR
- Performance-sensitive changes should be checked against baselines in `DataImportExportManager.Tests/SessionCacheBenchmarkBaselines.cs` where applicable

---

## Definition of Done

A change is considered complete when:

- [ ] Code builds with zero warnings and zero errors
- [ ] All tests pass (`dotnet test`)
- [ ] New/changed public APIs have XML doc comments
- [ ] `CHANGELOG.md` has an entry under `[Unreleased]`
- [ ] The PR has been reviewed and approved
- [ ] `pwsh ./scripts/validate.ps1 -Pack` succeeds locally
