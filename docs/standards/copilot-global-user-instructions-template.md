# Copilot Global User Instructions Template

Use this document as the reusable baseline for Copilot user-level instructions across repositories and future applications. Repository-local instruction files should contain only project-specific context, deployment details, and approved deviations.

## Engineering Standards
- Follow repository conventions first, then platform conventions.
- Prefer clean architecture with clear separation across API, orchestration, configuration, diagnostics, models, and infrastructure.
- Keep controllers thin and business behavior out of entry points.
- Use descriptive names for files, types, methods, tests, and configuration sections.
- Favor small, focused classes and methods.
- Use guard clauses early.
- Prefer immutable DTOs and records where practical.
- Do not widen visibility without need.
- Comments should explain why, not what.
- Move reusable user-facing strings to localizable resources when practical.

## SOLID and Dependency Rules
- Apply single responsibility to classes and services.
- Prefer extension over modification when introducing new provider behavior.
- Keep interfaces small and purposeful.
- Add abstractions only for external dependencies, seams, or testing.
- Do not wrap existing abstractions without a clear benefit.
- Reuse existing components before introducing new layers.
- Avoid speculative abstractions.

## Error Handling and Validation
- Use precise exception types.
- Validate inputs with guard clauses.
- Do not swallow exceptions silently.
- Centralize API error formatting where practical.
- Keep validation close to entry points or dedicated validation services.

## Security
- Secure by default.
- Do not store secrets in code.
- Use least privilege for identities and permissions.
- Keep tenant or client isolation explicit.
- Validate uploaded content before persistence.
- Keep authorization rules centralized and testable.

## Performance
- Use async end-to-end.
- Stream large payloads instead of buffering in memory.
- Optimize measured hot paths rather than guessing.
- Cache stable configuration and other safe low-volatility data selectively.
- Do not cache sensitive or tenant-scoped data without correct partitioning and invalidation.
- Avoid unbounded list and query patterns.
- Add explicit timeouts to external I/O.
- Retry only idempotent or safely repeatable operations.
- Measure latency, throughput, memory, retry, and timeout behavior.

## Observability and Operations
- Use structured logging with correlation context.
- Provide health and readiness endpoints where applicable.
- Emit telemetry for critical request and dependency flows.
- Add audit logging for important state-changing or access-sensitive actions.
- Use rate limiting or concurrency controls where shared resources are exposed.

## Testing
- Separate test projects from production projects.
- Name test files and test classes by subject under test.
- Follow Arrange-Act-Assert.
- Keep tests deterministic and independent.
- Add unit tests for changed public behavior.
- Add integration or contract tests for key endpoint flows.
- Avoid mocking internal implementation details when deterministic fakes or real public APIs are available.

## Code Smell Watchlist
- Entry-point files that grow into orchestration code.
- Infrastructure classes that combine transport, retry, telemetry, parsing, and policy logic without boundaries.
- Generic or catch-all test files.
- Configuration sprawl without grouping.
- Repeated string literals for keys, claims, or route fragments.
- New abstractions added without a present need.

## Repository-Local Addendum Rule
Each repository should keep a slim local instruction file that covers only:
- application purpose
- platform or tenant specifics
- cloud resource naming
- auth claim specifics
- approved provider scope
- explicit exceptions to the global standards
