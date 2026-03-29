# GitHub Copilot Instructions — DataImportExportManager

## Project Context

This is a **.NET 10** class library for importing and exporting tabular data between CSV and Excel (.xlsx) formats. The library's responsibility is exactly two things: (1) open and read a document stream, returning tabular data; (2) receive tabular data and write a document stream. Orchestration, format detection, and pipeline coordination are the consuming application's responsibility. GitHub Copilot is expected to generate, refactor, and review code with all of the conventions below in mind.

## Technology Stack

*   **.NET 10** — Target framework (`net10.0`); use modern C# features (file-scoped namespaces, raw strings, switch expressions, ranges/indices, primary constructors where appropriate).
*   **DocumentFormat.OpenXml 3.3.0** — Excel read/write.
*   **Microsoft.Extensions.Logging.Abstractions** — `ILogger<T>` via dependency injection. The library depends only on the abstractions package; consuming applications provide their own logging implementation.
*   **xUnit 2.9.3** — Unit testing framework.

## SOLID Principles

*   **Single Responsibility (SRP):** Each class has one clear purpose (import one format, or export one format). Never create God objects.
*   **Open/Closed (OCP):** New formats are added by implementing `IDataImporter` or `IDataExporter` — never by modifying existing classes.
*   **Liskov Substitution (LSP):** Subtypes must be substitutable for their base interfaces without breaking callers.
*   **Interface Segregation (ISP):** Import and export are separate interfaces. Never force a class to implement methods it does not use.
*   **Dependency Inversion (DIP):** Depend on abstractions (`IDataImporter`, `IDataExporter`, `ILogger<T>`), not concrete types. Use constructor injection.

## Project Conventions

### File & Folder Layout

*   Interfaces go in the `Interfaces/` folder with namespace `DataImportExportManager.Interfaces`.
*   Importers go in `Importers/`, exporters in `Exporters/`, DI extensions in `Extensions/`.
*   Options classes live alongside the component they configure (e.g., `CsvImporterOptions` in `Importers/`).
*   One public type per file.

### Naming

*   Private/internal fields: `_camelCase`.
*   Async methods **must** end with `Async`.
*   Interfaces start with `I` + PascalCase.
*   Constants: `PascalCase`.

### Visibility

*   Least-exposure rule: prefer `private` > `internal` > `protected` > `public`.
*   Classes are `sealed` unless designed for inheritance.

### Null Handling

*   Use `ArgumentNullException.ThrowIfNull(x)` for reference parameters.
*   Use `ArgumentException.ThrowIfNullOrWhiteSpace(x)` for string parameters.
*   Guard early at the top of public methods.

## Async & ValueTask Conventions

*   All async methods return `ValueTask` or `ValueTask<T>` (not `Task`).
*   Decorate async implementations with `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]` (or the non-generic variant for `ValueTask`).
*   Accept a `CancellationToken` on every async method and pass it through.
*   Use `ConfigureAwait(false)` in library code.
*   Never use fire-and-forget; always await.
*   Synchronous paths return `default` (e.g., `ExcelExporter`).

## Logging Conventions

*   Inject `ILogger<T>` via constructor on every service class.
*   Use `partial class` with `[LoggerMessage]` source-generated methods for zero-allocation structured logging.
*   **Log levels:**
    *   `Information` — Orchestrator-level events only (no orchestrator in this library; reserved for consumers).
    *   `Debug` — Component-level operations (importers/exporters).
    *   `Trace` — Verbose internals (e.g., buffering non-seekable streams).
*   Include `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()` timing in all import/export methods.
*   **Never** use format specifiers (`:F1`, `:N0`) in `[LoggerMessage]` templates — the source generator does not support them.
*   In unit tests, use `NullLogger<T>.Instance`.

## Security Conventions

*   **CSV injection:** `CsvExporterOptions.SanitizeFormulaCells` (default `false`) is the opt-in guard that prefixes formula-triggering cell values (`=`, `+`, `-`, `@`, `\t`, `\r`) with `'`. Do not apply sanitization unconditionally — the library must faithfully serialise what the consumer provides.
*   **Buffering guard:** Enforce a configurable maximum buffer size (default 100 MB via `ExcelImporterOptions`) using chunked copy with `ArrayPool<byte>` — never `CopyToAsync` before validating size.
*   **Stream disposal:** Wrap `MemoryStream` creation in `try`/`finally` to prevent leaks on rejection or cancellation paths.
*   Do not catch base `Exception`; use precise exception types.

## Performance Conventions

*   Use `SearchValues<char>` for SIMD-accelerated character scanning.
*   Reuse `StringBuilder` across iterations (clear, don't re-create).
*   Pre-allocate collections when count is known (`SharedStringTable.Count`, column count).
*   Use `ArrayPool<byte>.Shared` for temporary buffers; return in `finally`.
*   Stream large payloads; avoid buffering entire files in memory.

## Dependency Injection

*   Register all services via `ServiceCollectionExtensions.AddDataImportExportManager()`.
*   Register importers/exporters as `IDataImporter` / `IDataExporter` (multiple implementations).
*   `ExcelImporterOptions`, `CsvImporterOptions`, and `CsvExporterOptions` are each configurable via `Action<TOptions>` callbacks on `AddDataImportExportManager()`.
*   All constructors accept `ILogger<T>? logger = null`; pass `null` or omit to use `NullLogger<T>.Instance`.
*   **Library boundary:** This is a class library — it depends only on logging *abstractions*. The consuming application owns logging pipeline configuration (sinks, levels, Application Insights, etc.).
*   Do not add packages that dictate the consumer’s infrastructure (e.g., telemetry exporters, hosting, configuration providers).
*   Do not add orchestration or format-detection utilities — those are the consumer’s responsibility.

## Testing Conventions

*   Framework: **xUnit 2.9.3** with `[Fact]` and `[Theory]`/`[InlineData]`.
*   Test project: `DataImportExportManager.Tests`.
*   Mirror class names: `CsvImporter` → `CsvImporterTests`.
*   Name tests by behaviour: `WhenInputIsEmpty_ThenReturnsNoRows`.
*   Arrange-Act-Assert pattern; one assertion per test.
*   For `ValueTask`-returning methods, use `.AsTask()` when passing to `Assert.ThrowsAsync`.
*   No mocking of solution-internal code; mock only external dependencies.
*   Avoid `InternalsVisibleTo` — test through public APIs only.

## Documentation

*   XML documentation comments on all public types and members.
*   `<GenerateDocumentationFile>` is enabled in the project.
*   Comments explain **why**, not what.

## Prohibited Actions

*   Do not generate code that violates SOLID principles.
*   Do not introduce new NuGet packages without justification.
*   Do not use `Task` where `ValueTask` is the established convention.
*   Do not catch or throw base `Exception`.
*   Do not use `[LoggerMessage]` format specifiers.
*   Do not use `public` visibility by default.
*   Do not provide vague responses; be specific with code examples and explanations.
