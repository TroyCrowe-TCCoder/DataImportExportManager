# Repository Modernization Status Matrix

## Repository
- Name: `DataImportExportManager`
- Last updated: `2026-04-12`
- Branch: `master`
- Latest commit: `0e9313e`
- Working tree: clean

## Summary
Modernization and expansion work has progressed from CSV/Excel baseline to multi-format deterministic routing with strong diagnostics governance.

Implemented and verified in this repo:
- Deterministic format routing via `IDataFormatRouter` / `DataFormatRouter`
- Format coverage: CSV, TSV, JSON, NDJSON (incl. `.jsonl` alias), XML, XLSX
- Options surfaces for JSON/NDJSON/XML and DI callback wiring
- Strict XML contract profile support
- Centralized diagnostics metadata in `Diagnostics/ContractDiagnostics.cs`
- Router and handler diagnostics conformance tests
- README diagnostics code/operation tables with sync tests

Latest validation state:
- Tests: `176/176` passing
- Build: successful

## Status Matrix
| Area | Status | Evidence | Next focus |
|---|---|---|---|
| Standards and instructions | Complete | `.github/copilot-instructions.md` now contains project-specific guidance and execution rules. | Keep instructions synced with actual workflow changes. |
| Structure and naming | Complete | Library + test layout aligned (`DataImportExportManager` + `DataImportExportManager.Tests`) with deterministic router and per-format import/export implementations. | Preserve consistency for new format additions. |
| Configuration and options | Complete | Options classes and DI callbacks added for CSV/JSON/NDJSON/XML/Excel scenarios. | Add option-validation tests for any new options added later. |
| Security and data integrity | Complete (library scope) | Input guards, stream-size limits, XML/JSON validation, deterministic schema enforcement, and controlled diagnostics surface are in place. | Continue to keep security ownership in consuming apps for auth concerns. |
| Testing and quality | Complete | Broad xUnit coverage including contract, integration-style DI tests, diagnostics conformance tests, and docs-sync guards; latest run `176/176`. | Maintain coverage parity for each new public API/diagnostic code. |
| Observability and diagnostics | Complete | Structured diagnostics prefix (`[DIXMGR:...]`), centralized catalog (`ContractDiagnostics`), operation/code documentation, and conformance enforcement tests. | Keep catalog/tables/tests synchronized when adding codes. |
| Performance and resilience | Complete (current scope) | Async import/export paths, size guards, deterministic parsing/export behavior, and existing performance-conscious implementation patterns retained. | Add targeted benchmarks only if regression signal appears. |
| CI/CD and delivery | Partial | Local build + full test validation consistently pass; manual commit/push cadence in use. | Add/expand CI workflow to enforce test/build on PRs and branch protection. |

## Session Handoff / Resume Notes
To continue from this exact checkpoint:
1. Pull latest `master`.
2. Start next approved bundle from diagnostics backlog or CI/CD automation backlog.
3. Follow enforced execution cadence: implement -> validate -> commit -> push.

Useful verification commands:
- `dotnet test`
- `dotnet build`
- `git status --short`

## Next Planned Bundle Candidate
Add CI enforcement for the diagnostics contract:
1. Add/upgrade pipeline workflow to run `dotnet build` + `dotnet test` on PR and `master`.
2. Include diagnostics conformance tests in required checks.
3. Document required status checks in repository standards docs.

## References
- `docs/standards/repository-modernization-checklist.md`
- `docs/standards/existing-repositories-rollout-execution-plan.md`
- `docs/standards/targeted-repositories-modernization-backlog.md`
