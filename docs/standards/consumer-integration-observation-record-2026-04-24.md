# Consumer Integration Observation Record - 2026-04-24

## Metadata
- Date: 2026-04-24
- Environment (dev/test/prod-like): dev
- API application name: Pending (to be supplied during first consumer rollout)
- Feature branch / commit: feature/next-bundle-2-from-dev / 82f4f43
- Observer: Pending

## Event Mapping Verification
1. `EventName` -> API notification type mapping
   - Result: Pending integration evidence
   - Evidence: No consumer API rollout execution captured yet.
2. `Message` suitability for UI display or localization-key translation
   - Result: Pending integration evidence
   - Evidence: Consumer UX/localization pipeline not yet exercised in this repo checkpoint.
3. Schema mismatch event contains `DecisionCode` + actionable `AvailableActions`
   - Result: Pass (library-scope)
   - Evidence: `ValidateSchemaAsync(..., IDataImportExportEventPublisher, ...)` emits `schema.validation.mismatch` with decision context and actions; verified by test coverage.
4. Session lifecycle event correlation using `SessionId`
   - Result: Pass (library-scope)
   - Evidence: Session stored/consumed/removed notifications emit `SessionId` in both in-memory and distributed cache flows; verified by tests.
5. Required routing/filtering context coverage using existing fields (`Extension`, `TenantId`, `SubjectId`, `OccurredAtUtc`)
   - Result: Pending integration evidence
   - Evidence: Library emits fields where available; consumer routing requirements must be confirmed in first API rollout.

## Decision Gate
- Did any API/UI routing decision require a missing field from `DataImportExportEvent`?
  - Pending integration evidence
- If Yes, list each missing field and consumer scenario:
  - None identified yet (awaiting first consumer rollout data).

## Enrichment Recommendation
- Recommendation: Deferred pending first consumer rollout evidence.
- If enrichment required, proposed minimal field set:
  - TBD from observed consumer routing gaps.
- Why each field is required (consumer-evidence based):
  - TBD from observed consumer routing gaps.

## Follow-up Actions
- Action 1: Execute first API integration pass and populate all pending evidence fields.
- Action 2: Determine whether localization flow requires additional message-key fields beyond `Message`.
- Action 3: If any missing field is identified, define minimal enrichment delta and add tests/docs before implementation.
