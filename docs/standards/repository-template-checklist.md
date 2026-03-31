# Repository Template Checklist

Use this checklist when creating a new repository or solution so shared engineering standards and repo-specific addenda are applied consistently.

## Standards and Instructions
- Add `docs/standards/global-engineering-standards.md` or reference the shared organizational source.
- Add `docs/standards/copilot-global-user-instructions-template.md` or point developers to the global Copilot user instructions.
- Add `docs/standards/template-ready-baseline-guide.md` to document what future repositories must replace after template creation.
- Create `.github/copilot-instructions.md` with meaningful project-specific deviations only.
- Record only solution-specific cloud, auth, tenant, provider, or deployment deviations in the repo instruction file.

## Repository Structure
- Create a production project and a separate test project named `[ProjectName].Tests`.
- Group production code by concern such as `Controllers`, `Services`, `Options`, `Models`, `Diagnostics`, and `Exceptions`.
- Ensure test files are named by subject under test.
- Add a `docs` area for setup, runbooks, architecture notes, and operational guidance.

## Configuration and Secrets
- Use strongly typed options for configuration sections.
- Keep secrets out of source control.
- Use user secrets, environment variables, or cloud secret stores such as Key Vault.
- Define a configuration strategy for development, test, and production environments.

## Security and Auth
- Define authentication and authorization strategy up front.
- Record tenant-specific claims, roles, and auth provider constraints in the repo-local instruction file.
- Use least privilege for managed identities, app registrations, and service connections.
- Add validation rules for user input and uploaded content.

## Observability and Operations
- Add correlation identifiers.
- Add centralized error handling and `ProblemDetails` formatting where applicable.
- Add health and readiness endpoints for services.
- Add structured logging, traces, and metrics hooks.
- Define audit logging requirements for sensitive or state-changing operations.

## Performance and Resilience
- Prefer async end-to-end.
- Stream large payloads instead of buffering.
- Define timeout and retry policies for external I/O.
- Add rate limiting or concurrency controls where applicable.
- Define caching rules, especially tenant or client partitioning.
- Establish performance budgets and validation expectations.

## Testing and Quality Gates
- Add unit tests for changed public behavior.
- Add integration or contract tests for key endpoint flows.
- Ensure tests run in CI.
- Add build and test validation to the pipeline.
- Keep deterministic test doubles for external dependencies when real integrations are not required.

## Delivery and CI/CD
- Add repository pipeline YAML.
- Add service connections or deployment identities with least privilege.
- Document environment prerequisites.
- Document deployment steps, rollback basics, and operational ownership.
- Validate that the pipeline references committed files only.

## Repo-Local Deviation Checklist
Before finalizing `.github/copilot-instructions.md`, confirm each item is truly repo-specific:
- remote repository or hosting context
- app service or deployment target names
- tenant or auth claim names
- approved provider scope
- application integration relationships
- meaningful performance deviations
- explicit exceptions to the global standards
