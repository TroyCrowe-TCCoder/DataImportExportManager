# Repository Copilot Addendum Template

Use this file as the repository-local `.github/copilot-instructions.md` starting point. Keep only meaningful project-specific deviations here. Global rules should live in the shared standards documents and global Copilot user instructions.

## Shared Standards
Global reusable engineering standards live in `docs/standards/global-engineering-standards.md`.  
The reusable Copilot user-instructions template lives in `docs/standards/copilot-global-user-instructions-template.md`.  
This repository instruction file should contain only project-specific context, deployment details, and approved deviations from the shared baseline.

## General Guidelines
- Avoid repetitive recurrence prompts. Provide a direct remediation strategy and continue execution without conversational detours.
- Treat this repository as a utility/class library, not a standalone application. Prioritize security, performance, reliability, and correctness; avoid adding standalone app concerns unless explicitly requested.
- Do not add standalone-app concerns (hosting, controllers, health/readiness endpoints, deployment wiring) unless explicitly requested.
- Prioritize secure, performant, reliable, and correct library behavior.
- Handle Excel hidden/control characters and date-format quirks safely, with predictable normalization behavior to avoid downstream data quality issues.
- When reporting readiness for next steps, include a concise summary of what the next steps entail.
- Bundle all proposed choices into the next approved execution step and proceed automatically, avoiding option lists.
- After each approved bundle, check in and push changes to origin consistently.
- When requesting approval for the next bundle, provide a detailed description of the planned bundle before asking for approval.

## Project-Specific Deviations
- Repository remote or hosting context: Azure DevOps Git repository (`origin`: `https://dev.azure.com/tcrowe0170/_git/DataImportExportManager`).
- Application purpose or bounded context: Utility class library for tabular data import/export between CSV and Excel (`.xlsx`) formats.
- Tenant, platform, or cloud specifics: No tenant-bound runtime behavior in this repository; cloud concerns are handled by consuming applications.
- Auth claim names or identity-provider specifics: Not applicable in this library (no authentication/authorization surface).
- Approved provider scope: `DocumentFormat.OpenXml` for Excel processing; `Microsoft.Extensions.*` abstractions for DI/logging only.
- Deployment target names or environment constraints: Pack/consume as a .NET 10 class library; do not introduce app-host-specific wiring in this repo.
- Meaningful integration relationships or exceptions: Consumers own orchestration, persistence, transport, and security boundaries; this repo focuses on deterministic format translation and data fidelity.
- All importer/exporter selection must be deterministic; the UI provides the format, and this utility should resolve and execute the appropriate importer/exporter based on explicit format input.

## Repository Branching and Check-In Governance
- Branch flow is enforced as: `local branch` -> `remote branch` -> PR to `dev` -> PR to `master`.
- Contributors other than repository owner must open pull requests from their branch into `dev` only.
- Direct pushes to `dev` and `master` are disallowed for non-owner users.
- Merge from `dev` to `master` requires repository owner approval.
- `master` merge completion is expected to run the delivery workflow.

## Continuous Integration Preferences
- Run build/tests on `feature/*` pushes, `dev` pushes, and PRs targeting `dev` or `master`.
- Do not run test execution on `master` merge CI; run delivery packaging/publish on `master` merge.

## Repository-Specific Performance Additions
- Add only deviations from the global performance baseline.
- Example: cache only tenant-scoped configuration by `clientId`.
- Example: do not cache content payloads.
