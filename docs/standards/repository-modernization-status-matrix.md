# Repository Modernization Status Matrix

## Repository
- Name: `DataImportExportManager`
- Last updated: `2026-04-23`
- Branch: `master`
- Latest commit: `7e405e0`
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
- Tests: `229/229` passing
- Build: successful

Latest session checkpoint outcomes:
- Schema mismatch decision support implemented (`ValidateSchema`, `SchemaValidationResult`, `SchemaMismatchAction`) with deterministic remap-required signaling.
- Tenant/subject-scoped temporary import session caching implemented (`IImportSchemaSessionCache`, `InMemoryImportSchemaSessionCache`) to support continue-with-remap without file reupload.
- Distributed cache session support implemented (`DistributedImportSchemaSessionCache`, `AddDistributedImportSchemaSessionCache`) for multi-instance hosting.
- Distributed session cache hardening implemented with configurable key-prefix isolation and max serialized payload guard rails (`DistributedImportSchemaSessionCacheOptions`) plus deterministic serialization/payload-limit failures.
- Session-cache performance visibility implemented with baseline threshold constants and benchmark-style store/get/consume coverage tests for in-memory and distributed cache flows.
- Event-driven notification hooks implemented for router and session-cache lifecycle events via `IDataImportExportEventPublisher` with default no-op publisher and API override support.
- DI-safe schema-validation notification path implemented via `ValidateSchemaAsync(..., IDataImportExportEventPublisher, ...)` overloads to emit schema-mismatch events through the shared publisher abstraction.
- Event contract field sufficiency reviewed for API toast-notification usage; current payload shape supports lifecycle status, scope identity, schema decision context, and user-facing message projection without additional required fields.
- Test project and test files are now located inside repository root (`DataImportExportManager.Tests`) to keep clone-local build/test extensibility.
- Branch governance finalized as PR-only (`dev` -> `master`) with owner-required approval and no second approver requirement.

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
| Session cache lifecycle and hardening | Complete | Distributed session cache now supports configurable key-prefix isolation and max payload guard rails with deterministic behavior; tests cover limits and isolation scenarios. | Add explicit consumer-facing docs snippet for distributed cache option tuning examples. |
| Event-driven integration hooks | Complete | `IDataImportExportEventPublisher` and lifecycle event contracts are wired through router and session caches with DI override support and no-op default implementation. | Evaluate DI-safe schema-validation event publishing path in a future refinement bundle. |
| Schema validation notification publishing | Complete | Async schema validation overloads publish deterministic schema-mismatch events through `IDataImportExportEventPublisher`; tests cover mismatch publish vs match no-publish behavior. | Keep event payload fields aligned with API toast/notification contracts. |
| Security and data integrity | Complete (library scope) | Input guards, stream-size limits, XML/JSON validation, deterministic schema enforcement, and controlled diagnostics surface are in place. | Continue to keep security ownership in consuming apps for auth concerns. |
| Testing and quality | Complete | Broad xUnit coverage including contract, integration-style DI tests, diagnostics conformance tests, docs-sync guards, and session-cache performance baseline tests; latest run `229/229`. | Maintain coverage parity for each new public API/diagnostic code. |
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
1. Pull latest `dev`.
2. Confirm no pending PR bundles remain before starting the next branch from `dev`.
3. Follow enforced execution cadence: implement -> validate -> commit -> push.
4. For schema-mismatch UX, consume `SchemaValidationResult.AvailableActions` and `IImportSchemaSessionCache` session IDs to offer "correct file" vs "continue with remap" without reupload.

Current in-flight PR state at this checkpoint:
- No active PR from `feature/next-bundle-2-from-dev` -> `dev` at this checkpoint (latest bundle merged to `dev` commit `1f93795`).

Useful verification commands:
- `dotnet test`
- `dotnet build`
- `git status --short`

## Next Planned Bundle Candidate
Project closeout hardening:
1. Complete pending evidence fields in `docs/standards/consumer-integration-observation-record-2026-04-24.md` during first API rollout.
2. Confirm whether event payload enrichment is unnecessary or define a minimal enrichment delta.
3. If enrichment is required, implement only consumer-validated fields and extend tests/docs accordingly.

## Planning Continuity Ledger
Completed planning bundles (preserve for next-session continuity):
1. Session-cache hardening and lifecycle controls
   - distributed payload-size guard rails
   - key-prefix isolation
   - deterministic failure behavior
2. Session-cache performance visibility
   - baseline thresholds
   - benchmark-style tests
   - maintainer execution guidance
3. Event-driven integration hooks
   - lifecycle event contracts and publisher abstraction
   - router/session emission paths
   - DI wiring and API override support
4. DI-safe schema validation notifications
   - async publisher-aware schema validation overloads
   - mismatch-event emission tests
5. Project closeout hardening
   - event contract sufficiency audit
   - explicit completion recommendation + non-blocking backlog

Planned-but-not-started items to retain in backlog:
- Complete first consumer API integration evidence capture (pending fields in 2026-04-24 observation record).
- Introduce optional event payload enrichers only if integration evidence demonstrates need.

## Next Session Execution Targets
- Owner: API integration workstream lead (assign at session start)
- Primary artifact to update: `docs/standards/consumer-integration-observation-record-2026-04-24.md`
- Timebox: 1 focused session (capture evidence + enrichment decision)
- Definition of done:
  1. All pending evidence fields in the dated observation record are resolved.
  2. Enrichment decision is explicit ("no enrichment" or minimal field set with rationale).
  3. If enrichment is required, create implementation task list with tests/docs scope.

## Project Completion Recommendation
- Recommendation: Ready to close as complete for current library scope.
- Non-blocking follow-up backlog:
  - Observe first API integration rollout for any additional toast-routing metadata needs.
  - Consider optional enrichers (for example correlation IDs) only after concrete consumer requirements are validated.

## Consumer Integration Observation Checklist (First Rollout)
Use this checklist during the first API integration pass to decide whether additional event payload fields are needed:

1. Verify every emitted `EventName` maps to a deterministic API notification type.
2. Verify `Message` values are suitable for UI display or localization-key translation.
3. Verify schema mismatch events include `DecisionCode` and actionable `AvailableActions` for remap UX.
4. Verify session lifecycle events include `SessionId` for retry/resume correlation.
5. Verify no required API/UI routing decision depends on fields missing from `DataImportExportEvent`.

Add payload enrichers only if checklist item 5 fails in real consumer integration evidence.

## Consumer Integration Evidence Records
- `docs/standards/consumer-integration-observation-record-2026-04-24.md` (kickoff baseline; pending API rollout evidence fields remain)

## References
- `docs/standards/repository-modernization-checklist.md`
- `docs/standards/existing-repositories-rollout-execution-plan.md`
- `docs/standards/targeted-repositories-modernization-backlog.md`
- `docs/standards/azure-devops-branch-policy-checklist.md`
- `docs/standards/consumer-integration-observation-record-template.md`
- `docs/standards/consumer-integration-observation-record-2026-04-24.md`
