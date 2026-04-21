# Changelog

All notable changes to **DataImportExportManager** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- `Contracts/TabularImportResult` to expose imported field names (`Columns`), preserve full imported rows including header (`Rows`), and provide data-only rows (`DataRows`).
- `DataImportSchemaExtensions.ImportWithSchemaAsync(...)` for both `IDataImporter` and `IDataFormatRouter` to standardize first-row header extraction across formats.
- `DataImportSchemaExtensions.ImportWithSchemaTupleAsync(...)` for both `IDataImporter` and `IDataFormatRouter` to return `(Headers, DataRows)` tuple results.
- `DataImportSchemaExtensions.ImportWithSchemaBundleAsync(...)` for both `IDataImporter` and `IDataFormatRouter` to return both `TabularImportResult` and `(Headers, DataRows)` from a single import call.
- `Contracts/SchemaValidationResult` and `Contracts/SchemaMismatchAction` for deterministic schema-match outcomes, remap-required signaling, and caller action options.
- `Contracts/ImportSchemaSession` and `IImportSchemaSessionCache` for temporary tenant-scoped caching of imported payloads during schema decision workflows.
- `DataImportSchemaExtensions.ValidateSchema(...)` overloads for validating imported headers against expected schema with missing/extra/duplicate diagnostics.
- `DataImportSchemaExtensions.CreateExampleImportFileAsync(...)` to generate downloadable example/template files per supported format using router-selected exporters.
- `InMemoryImportSchemaSessionCache` and `AddImportSchemaSessionCache(...)` for short-lived no-reupload continue-with-remap flows.
- `TsvImporter` and `TsvExporter` for deterministic `.tsv` support.
- `JsonImporterOptions`, `JsonExporterOptions`, `NdjsonImporterOptions`, `NdjsonExporterOptions`, `XmlImporterOptions`, and `XmlExporterOptions` for configurable format behavior with deterministic defaults.
- `.jsonl` alias support in `DataFormatRouter` (normalized to `.ndjson`).
- `XmlImporter`/`XmlExporter` object-element row mode support:
  - Import: `<rows><row><name>value</name></row></rows>` with deterministic header synthesis.
  - Export: optional first-row-as-headers mapping to object elements.
- XML strict contract profile enhancements:
  - Importer `XmlImporterOptions.RowSchemaMode` (`Auto`, `CellsOnly`, `ObjectElementsOnly`) with row-index diagnostics.
  - Exporter `XmlExporterOptions.StrictObjectElementRowWidth` enforcing header/data width alignment with row diagnostics.
- Router diagnostics normalization:
  - `IDataFormatRouter` now wraps contract failures with deterministic prefixes: `[DIXMGR:<extension>:<operation>:<code>]`.
  - Route-not-found failures now use `ROUTE_NOT_FOUND` diagnostic code for stable handling.
- Diagnostics parity completion for CSV/TSV and Excel:
  - CSV/TSV delimiter validation now emits `INVALID_DELIMITER`.
  - Excel importer emits `INVALID_WORKBOOK`, `SHEET_NOT_FOUND`, and `BUFFER_LIMIT_EXCEEDED`.
  - Excel exporter emits `INVALID_SHEET_NAME` and `ROW_LIMIT_EXCEEDED`.
- Diagnostics parity refinements:
  - JSON and NDJSON malformed payload parsing now emits `MALFORMED_JSON`.
  - XML malformed payload parsing now emits `MALFORMED_XML`.
  - Excel importer negative sheet-index validation now emits `INVALID_SHEET_INDEX`.
  - Added README diagnostics code reference table for consumer integration.
- Diagnostics conformance hardening:
  - Router duplicate importer/exporter registration now emits `DUPLICATE_HANDLER`.
  - Added centralized diagnostics conformance matrix tests to assert stable DIXMGR message shape across direct handlers and router paths.
- Diagnostics metadata consolidation:
  - Added centralized internal diagnostics catalog constants for operations and codes.
  - Refactored handlers and router to use the catalog to reduce code drift risk.
- Diagnostics catalog hardening:
  - Added operation-scoped message builders (`BuildImportMessage`, `BuildExportMessage`, `BuildConfigMessage`).
  - Added catalog helper checks for known operations/codes and input validation guards in diagnostics message construction.
  - Diagnostics message construction now rejects unknown operation/code values to enforce strict catalog integrity.
  - Added diagnostics conformance tests for catalog recognition and unknown operation/code rejection behavior.
- New test suites and integration coverage:
  - `TsvImporterTests`, `TsvExporterTests`
  - `XmlImporterTests`, `XmlExporterTests`
  - `ServiceCollectionExtensionsTests`, `FormatRouterContractTests`

- `CsvImporterOptions` — Configurable `Encoding` (default UTF-8) and `Delimiter` (default `,`) for `CsvImporter`.
- `CsvExporterOptions` — Configurable `Encoding` (default UTF-8, no BOM), `Delimiter`, and opt-in `SanitizeFormulaCells` flag for `CsvExporter`.
- `ExcelImporterOptions.SheetName` — Import a specific worksheet by name (case-insensitive).
- `ExcelImporterOptions.SheetIndex` — Import a specific worksheet by zero-based index.
- `AddDataImportExportManager()` now accepts `Action<CsvImporterOptions>` and `Action<CsvExporterOptions>` configuration callbacks.
- Unit tests for multi-line quoted fields, CRLF line endings, custom delimiters, opt-in sanitization, sheet selection by name and index (39 tests total).

### Fixed

- **CSV multi-line quoted field bug** — `CsvImporter` previously used `ReadLineAsync`, which split RFC 4180 multi-line quoted fields across multiple rows, silently corrupting data. The parser now reads character-by-character using `ArrayPool<char>` chunks and maintains state across OS line boundaries, correctly handling `\r\n`, `\r`, and `\n` inside quoted fields per RFC 4180 §2.6.

### Changed

- `DataFormatRouter` now wraps previously unprefixed handler `ArgumentException` failures during routed import/export with deterministic diagnostics (`[DIXMGR:<extension>:CONFIG:CONTRACT]`) while preserving inner exception context.
- JSON and XML malformed import diagnostics now include deterministic parser location context (line and byte/position) when available, while preserving existing diagnostic codes.
- NDJSON malformed import diagnostics now include deterministic parser byte-position context alongside source line number while preserving existing diagnostic codes.
- All constructors accept optional parameters: `ILogger<T>? logger = null` falls back to `NullLogger<T>.Instance`; options classes default to sensible values. Manual construction no longer requires any arguments (e.g., `new CsvImporter()`).
- `CsvExporter` CSV injection sanitization is now **opt-in** via `CsvExporterOptions.SanitizeFormulaCells` (default `false`). Previously sanitization was always applied, silently mutating consumer data.
- `ExcelImporter` stores the full `ExcelImporterOptions` object instead of only `MaxBufferSize`.
- `CsvExporterOptions.Encoding` defaults to `UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` (no BOM) to match `StreamWriter` default behaviour and avoid unexpected BOM bytes in output.

### Removed

- `DataPipelineService` — Format conversion orchestration is the consuming application's responsibility; consumers compose `ImportAsync` + `ExportAsync` directly.
- `FileFormatDetector` and `IFileFormatDetector` — `DetectExtension` was a thin wrapper over `Path.GetExtension()`; this trivial utility belongs in the consumer.
- `DataPipelineServiceTests` and `FileFormatDetectorTests` — Tests for removed components.


- `IDataExporter` interface for format-specific tabular-data-to-stream export.
- `IFileFormatDetector` interface for file extension detection and support queries.
- `CsvImporter` — RFC 4180–compliant CSV parser with quoted field and escaped quote support.
- `CsvExporter` — CSV writer with automatic field escaping for commas, quotes, and newlines.
- `ExcelImporter` — `.xlsx` reader using DocumentFormat.OpenXml with shared string table support.
- `ExcelExporter` — `.xlsx` writer using DocumentFormat.OpenXml that creates a single-sheet workbook.
- `DataPipelineService` — Orchestrates format-to-format conversion via registered importers/exporters.
- `FileFormatDetector` — Resolves file extensions and queries registered format support.
- `ServiceCollectionExtensions` — DI registration via `AddDataImportExportManager()` with optional `Action<ExcelImporterOptions>` configuration callback.
- `ExcelImporterOptions` — Configurable buffer size limits for `ExcelImporter` (default 100 MB, adjustable per hosting tier).
- Unit tests for `CsvImporter`, `CsvExporter`, `ExcelImporter`, `FileFormatDetector`, and `DataPipelineService` (47 tests).
- XML documentation on all public types and members.
- `GenerateDocumentationFile` enabled in the project file.
- Comprehensive `README.md` with usage examples, DI setup, project structure, and extensibility guide.
- `CONTRIBUTING.md` — Contribution guidelines, branching strategy, and coding standards.
- `SECURITY.md` — Vulnerability reporting policy and current security measures.
- `LICENSE` — MIT license.
- `.azuredevops/pull_request_template.md` — PR template with checklist.
- `.editorconfig` — Code style, naming conventions, and analyzer severities.
- `Directory.Build.props` — Shared MSBuild properties (TreatWarningsAsErrors, AnalysisLevel, Nullable).
- `global.json` — Pins .NET 10 SDK version.

### Security

- CSV injection prevention — Cell values starting with formula-triggering characters (`=`, `+`, `-`, `@`, `\t`, `\r`) are sanitized with a `'` prefix during export.
- Stream buffering size guard — `ExcelImporter` enforces a configurable maximum (default 100 MB) via chunked `ArrayPool<byte>` copy to prevent denial-of-service from oversized payloads.
- Shared string table bounds checking — Out-of-range shared string indices return empty strings instead of throwing.
- `MemoryStream` resource leak fix — `try`/`finally` wrapping ensures disposal on rejection or cancellation paths.
- Input validation — All public method parameters guarded with `ArgumentNullException.ThrowIfNull()` and `ArgumentException.ThrowIfNullOrWhiteSpace()`.

### Performance

- `ValueTask` / `ValueTask<T>` with `PoolingAsyncValueTaskMethodBuilder` on all async methods for reduced allocations.
- `SearchValues<char>` SIMD-accelerated character scanning in `CsvExporter`.
- `StringBuilder` reuse across iterations in both `CsvImporter` and `CsvExporter`.
- Collection pre-allocation using `SharedStringTable.Count` and column-count hints.
- `ArrayPool<byte>.Shared` for temporary buffers with `finally`-guaranteed return.
- `Stopwatch.GetTimestamp()` / `GetElapsedTime()` timing in all import/export methods.
- `ConfigureAwait(false)` on all `await` calls to prevent deadlocks in consumer synchronization contexts.

### Observability

- `ILogger<T>` injected via constructor on all six service classes.
- `[LoggerMessage]` source-generated methods for zero-allocation structured logging.
- Log level tiers: `Information` for orchestrator, `Debug` for components, `Trace` for verbose internals.
- Library depends only on `Microsoft.Extensions.Logging.Abstractions` — consuming applications configure their own logging pipeline.

### Changed

- `ExcelImporter` constructor now accepts `ExcelImporterOptions` for configurable buffer size limits.
- `AddDataImportExportManager()` accepts an optional `Action<ExcelImporterOptions>` callback.
- Replaced `Microsoft.Extensions.Logging` (implementation) with `Microsoft.Extensions.Logging.Abstractions` (abstractions only).
- Added `Microsoft.Extensions.DependencyInjection.Abstractions` as an explicit dependency.

### Removed

- `Azure.Monitor.OpenTelemetry.Exporter` package — telemetry sink configuration is the consuming application's responsibility, not the library's.
- `AddAzureMonitorLogging()` extension method — consumers configure their own logging pipeline via `AddLogging()` in their host.
