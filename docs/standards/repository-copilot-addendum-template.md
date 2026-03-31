# Repository Copilot Addendum Template

Use this file as the repository-local `.github/copilot-instructions.md` starting point. Keep only meaningful project-specific deviations here. Global rules should live in the shared standards documents and global Copilot user instructions.

## Shared Standards
Global reusable engineering standards live in `docs/standards/global-engineering-standards.md`.
The reusable Copilot user-instructions template lives in `docs/standards/copilot-global-user-instructions-template.md`.
This repository instruction file should contain only project-specific context, deployment details, and approved deviations from the shared baseline.

## Project-Specific Deviations
- Repository remote or hosting context: `<replace>`
- Application purpose or bounded context: `<replace>`
- Tenant, platform, or cloud specifics: `<replace>`
- Auth claim names or identity-provider specifics: `<replace>`
- Approved provider scope: `<replace>`
- Deployment target names or environment constraints: `<replace>`
- Meaningful integration relationships or exceptions: `<replace>`

## Repository-Specific Performance Additions
- Add only deviations from the global performance baseline.
- Example: cache only tenant-scoped configuration by `clientId`.
- Example: do not cache content payloads.
