# Consumer Integration Observation Record Template

Use this template during the first API integration rollout to capture deterministic evidence for whether `DataImportExportEvent` payload enrichment is required.

## Metadata
- Date:
- Environment (dev/test/prod-like):
- API application name:
- Feature branch / commit:
- Observer:

## Event Mapping Verification
1. `EventName` -> API notification type mapping
   - Result: Pass / Fail
   - Evidence:
2. `Message` suitability for UI display or localization-key translation
   - Result: Pass / Fail
   - Evidence:
3. Schema mismatch event contains `DecisionCode` + actionable `AvailableActions`
   - Result: Pass / Fail
   - Evidence:
4. Session lifecycle event correlation using `SessionId`
   - Result: Pass / Fail
   - Evidence:
5. Required routing/filtering context coverage using existing fields (`Extension`, `TenantId`, `SubjectId`, `OccurredAtUtc`)
   - Result: Pass / Fail
   - Evidence:

## SaaS Configuration Boundary Verification
1. External-service integrations are configured via hosting API passthrough (not hard-coded in library)
   - Result: Pass / Fail
   - Evidence:
2. Tenant-specific versus environment-global host settings are explicitly documented
   - Result: Pass / Fail
   - Evidence:
3. Library self-hosted options exposed for tenant or deployment tuning are documented for operators
   - Result: Pass / Fail
   - Evidence:
4. Event transport integration path from API to UI notification channel is documented
   - Result: Pass / Fail
   - Evidence:

## Decision Gate
- Did any API/UI routing decision require a missing field from `DataImportExportEvent`?
  - Yes / No
- If Yes, list each missing field and consumer scenario:

## Enrichment Recommendation
- Recommendation: No enrichment required / Minimal enrichment required
- If enrichment required, proposed minimal field set:
- Why each field is required (consumer-evidence based):

## Follow-up Actions
- Action 1:
- Action 2:
- Action 3:
