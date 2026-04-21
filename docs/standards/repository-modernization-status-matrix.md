# Repository Modernization Status Matrix

## Repository
- Name: `DataImportExportManager`
- Last updated: `2026-04-21`
- Branch: `master`
- Latest commit: `08c5b67`
- Working tree at checkpoint: clean

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
- Tests: `200/200` passing
- Build: successful

## Staleness Audit Checkpoint
- Scope executed: stale-code signals, markdown link integrity, and cross-document flow/governance drift.
- Result: no stale-code markers found (`TODO`/`FIXME`/`[Obsolete]` scans returned no actionable findings).
- Result: no missing local markdown links detected across repository markdown files.
- Result: governance and CI flow wording is synchronized across `README.md`, `.github/copilot-instructions.md`, and standards docs.

## Metadata Maintenance Notes
- Keep `Last updated` in strict `YYYY-MM-DD` format.
- Keep `Latest commit` synchronized to current checkpoint commit; CI allows current `HEAD` or immediate parent commit during in-flight updates.

## CI Guard Inventory (Current)
- `validate modernization status metadata` -> `scripts/ci/Validate-ModernizationStatusMetadata.ps1`
- `validate markdown links` -> `scripts/ci/Validate-MarkdownLinks.ps1`
- `validate docs and pipeline contracts` -> `scripts/ci/Validate-DocsAndPipelineContracts.ps1`

## Maintenance Cadence
- Per approved bundle: run guard scripts, `dotnet build`, and `dotnet test` before commit/push.
- Weekly: verify branch policy configuration still matches `docs/standards/azure-devops-branch-policy-checklist.md`.
- Monthly: refresh this matrix checkpoint metadata (`Last updated`, `Latest commit`, validation counters) and re-run staleness audit scan.

## Status Matrix
| Area | Status | Evidence | Next focus |
|---|---|---|---|
| Standards and instructions | Complete | `.github/copilot-instructions.md` now contains project-specific guidance and execution rules. | Keep instructions synced with actual workflow changes. |
| Structure and naming | Complete | Library + test layout aligned (`DataImportExportManager` + `DataImportExportManager.Tests`) with deterministic router and per-format import/export implementations. | Preserve consistency for new format additions. |
| Configuration and options | Complete | Options classes and DI callbacks added for CSV/JSON/NDJSON/XML/Excel scenarios. | Add option-validation tests for any new options added later. |
| Security and data integrity | Complete (library scope) | Input guards, stream-size limits, XML/JSON validation, deterministic schema enforcement, and controlled diagnostics surface are in place. | Continue to keep security ownership in consuming apps for auth concerns. |
| Testing and quality | Complete | Broad xUnit coverage including contract, integration-style DI tests, diagnostics conformance tests, and docs-sync guards; latest run `200/200`. | Maintain coverage parity for each new public API/diagnostic code. |
| Observability and diagnostics | Complete | Structured diagnostics prefix (`[DIXMGR:...]`), centralized catalog (`ContractDiagnostics`), operation/code documentation, and conformance enforcement tests. | Keep catalog/tables/tests synchronized when adding codes. |
| Performance and resilience | Complete (current scope) | Async import/export paths, size guards, deterministic parsing/export behavior, and existing performance-conscious implementation patterns retained. | Add targeted benchmarks only if regression signal appears. |
| CI/CD and delivery | Complete (library scope) | Azure DevOps validation workflow at `.azure-pipelines/workflows/dataimportexportmanager-ci.yml` validates `feature/*`/`dev` flow and PRs; branch policies enforce PR-only promotion to `master` with no direct contributor pushes to `dev` or `master`. | Keep policy and validation script drift checks synchronized with actual workflow. |

## Required CI Status Checks (Azure DevOps)
- Require successful run of `dataimportexportmanager-ci.yml` validation stage for pull requests targeting `dev` and `master`.
- Require successful completion of `validate modernization status metadata` step in the validation stage.
- Require successful completion of `validate markdown links` step in the validation stage.
- Require successful completion of `validate docs and pipeline contracts` drift guard step in the validation stage.
- Require successful completion of validation steps: `dotnet restore`, `dotnet build`, and `dotnet test`.
- Keep diagnostics conformance coverage enforced through the existing test suite included in `dotnet test`.
- Do not require a standalone delivery stage on `master` for this class-library repository.

## Required Branch Governance (Azure DevOps)
- Enforce contributor workflow: local branch pushed to remote branch, then pull request into `dev` only.
- Enforce promotion workflow: `dev` pull requests into `master` only.
- Disallow direct pushes to `dev` and `master` for non-owner users.
- Require repository owner approval before completing `dev` -> `master` pull requests.

## Session Handoff / Resume Notes
To continue from this exact checkpoint:
1. Pull latest `master`.
2. Start next approved bundle from diagnostics backlog or documentation/guard drift backlog.
3. Follow enforced execution cadence: implement -> validate -> commit -> push.

Useful verification commands:
- `dotnet test`
- `dotnet build`
- `git status --short`

## Next Planned Bundle Candidate
Policy/guard drift hardening:
1. Keep `README.md`, `.github/copilot-instructions.md`, and branch-policy checklist wording synchronized around PR-only governance.
2. Periodically verify Azure DevOps policy IDs and settings remain present (`dev` + `master` reviewer/comment policies and master required reviewer).
3. Re-run docs/metadata guard scripts and refresh this matrix metadata after each approved governance/docs bundle.

## References
- `docs/standards/repository-modernization-checklist.md`
- `docs/standards/existing-repositories-rollout-execution-plan.md`
- `docs/standards/targeted-repositories-modernization-backlog.md`
- `docs/standards/azure-devops-branch-policy-checklist.md`
