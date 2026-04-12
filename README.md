# DataImportExportManager

A .NET 10 library for deterministic tabular data import/export across CSV, Excel (.xlsx), JSON, and NDJSON (.ndjson/.jsonl) formats.

## Features

- **CSV/TSV import/export** — RFC 4180–compliant parsing and writing with full support for multi-line quoted fields, configurable delimiter, and configurable encoding.
- **Excel import/export** — Reads and writes `.xlsx` files via the [DocumentFormat.OpenXml](https://www.nuget.org/packages/DocumentFormat.OpenXml) SDK with configurable sheet selection.
- **JSON import/export** — Supports `.json` payloads using `System.Text.Json` with deterministic tabular mapping.
- **NDJSON import/export** — Supports line-delimited JSON records (`.ndjson` and `.jsonl` alias) for pipeline-friendly ingestion/export.
- **XML import/export** — Supports deterministic tabular XML using `<rows><row><cell>...</cell></row></rows>` schema, with optional object-element row mode.
- **Deterministic format routing** — `IDataFormatRouter` resolves importers/exporters from explicit format input (for example, UI-selected extension).

### JSON Serializer Baseline

This library uses `System.Text.Json` (Microsoft .NET JSON APIs) and does not depend on `Newtonsoft.Json`.
- **Extensible** — Add new formats by implementing `IDataImporter` or `IDataExporter` and registering them with the DI container.
- **Structured logging** — Zero-allocation `[LoggerMessage]` source-generated logging with `ILogger<T>`. All loggers are optional — the library falls back to `NullLogger<T>` automatically.
- **Configurable options** — Per-component options classes let consumers tune encoding, delimiter, buffer limits, sheet selection, and sanitization behaviour.
- **Security hardening** — Opt-in formula-cell sanitization, configurable stream size limits, and resource leak protection.
- **High performance** — `SearchValues<char>` SIMD scanning, `ArrayPool<byte/char>` buffers, `ValueTask` with pooling, and pre-allocated collections.

## Project Structure

```
DataImportExportManager/
├── Interfaces/                  # Public contracts
│   ├── IDataImporter.cs         # Stream → tabular data
│   ├── IDataExporter.cs         # Tabular data → stream
│   └── IDataFormatRouter.cs     # Deterministic format selection
├── Importers/                   # IDataImporter implementations
│   ├── CsvImporter.cs
│   ├── CsvImporterOptions.cs
│   ├── TsvImporter.cs
│   ├── ExcelImporter.cs
│   ├── ExcelImporterOptions.cs
│   ├── JsonImporter.cs
│   ├── NdjsonImporter.cs
│   ├── XmlImporter.cs
│   ├── XmlImporterOptions.cs
│   └── JsonImporterOptions.cs
├── Exporters/                   # IDataExporter implementations
│   ├── CsvExporter.cs
│   ├── CsvExporterOptions.cs
│   ├── TsvExporter.cs
│   ├── ExcelExporter.cs
│   ├── JsonExporter.cs
│   ├── JsonExporterOptions.cs
│   ├── NdjsonExporter.cs
│   ├── NdjsonExporterOptions.cs
│   ├── XmlExporter.cs
│   └── XmlExporterOptions.cs
├── Services/                    # Routing/orchestration
│   └── DataFormatRouter.cs
└── Extensions/                  # DI registration
    └── ServiceCollectionExtensions.cs

DataImportExportManager.Tests/   # xUnit test project
├── CsvImporterTests.cs
├── CsvExporterTests.cs
├── ExcelImporterTests.cs
├── JsonImporterTests.cs
├── JsonExporterTests.cs
├── NdjsonImporterTests.cs
├── NdjsonExporterTests.cs
└── DataFormatRouterTests.cs
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned via `global.json`)

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Branch Governance and Delivery Flow

Repository governance follows:
`local branch -> remote branch -> PR to dev -> PR to master -> delivery on master merge`.

For required Azure DevOps branch-policy settings and operator steps, see:
`docs/standards/azure-devops-branch-policy-checklist.md`.

## Usage

### Dependency Injection (Recommended)

Register all services in your DI container:

```csharp
using DataImportExportManager.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Register importers and exporters with default options
builder.Services.AddDataImportExportManager();

// Or configure individual components
builder.Services.AddDataImportExportManager(
    configureExcelImporter: excel =>
    {
        excel.MaxBufferSize = 25 * 1024 * 1024; // 25 MB
        excel.SheetName = "Data";               // import a named sheet
    },
    configureCsvImporter: csv =>
    {
        csv.Delimiter = ';';                    // semicolon-delimited
    },
    configureCsvExporter: csv =>
    {
        csv.SanitizeFormulaCells = true;        // enable injection protection
    },
    configureXmlExporter: xml =>
    {
        xml.UseObjectElementRows = true;        // first row mapped as XML element names
        xml.StrictObjectElementRowWidth = true; // enforce header/data width contract
    },
    configureXmlImporter: xml =>
    {
        xml.RowSchemaMode = XmlImportRowSchemaMode.ObjectElementsOnly; // strict object-row profile
    });
```

Resolve and route by explicit extension (recommended for UI-selected formats):

```csharp
// Inject IDataFormatRouter
var importer = router.GetImporter(".csv");
var exporter = router.GetExporter("xlsx");

await using var source      = File.OpenRead("data.csv");
await using var destination = File.Create("data.xlsx");

var rows = await importer.ImportAsync(source);
await exporter.ExportAsync(rows, destination);
```

### Manual Construction (No DI)

All constructors accept optional parameters — no arguments are required:

```csharp
var csvImporter  = new CsvImporter();
var excelImporter = new ExcelImporter();
var csvExporter  = new CsvExporter();
var excelExporter = new ExcelExporter();

// With options
var tsvImporter = new CsvImporter(new CsvImporterOptions { Delimiter = '\t' });
var sanitizingExporter = new CsvExporter(new CsvExporterOptions { SanitizeFormulaCells = true });
var secondSheetImporter = new ExcelImporter(new ExcelImporterOptions { SheetIndex = 1 });
```

### Import CSV Data Directly

```csharp
await using var stream = File.OpenRead("data.csv");
IReadOnlyList<IReadOnlyList<string>> rows = await new CsvImporter().ImportAsync(stream);
```

## Configuration Reference

### `CsvImporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `Encoding` | UTF-8 | Text encoding for reading the stream |
| `Delimiter` | `,` | Field separator character |

### `CsvExporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `Encoding` | UTF-8 (no BOM) | Text encoding for writing the stream |
| `Delimiter` | `,` | Field separator character |
| `SanitizeFormulaCells` | `false` | Prefix formula-triggering chars (`=`,`+`,`-`,`@`,`\t`,`\r`) with `'` |

### `ExcelImporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `MaxBufferSize` | 100 MB | Maximum bytes to buffer for non-seekable streams |
| `SheetName` | `null` | Import a worksheet by name (case-insensitive); takes precedence over `SheetIndex` |
| `SheetIndex` | `null` | Import a worksheet by zero-based index |

### `JsonImporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `IncludeHeaderRowForObjectRecords` | `true` | For object-record JSON arrays, includes a synthesized deterministic header row |

### `JsonExporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `WriteIndented` | `false` | Writes indented JSON output when enabled |

### `NdjsonImporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `IgnoreBlankLines` | `true` | Ignores blank/whitespace NDJSON lines; throws when disabled |

### `NdjsonExporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `WriteTrailingNewline` | `true` | Writes a final trailing newline after last NDJSON record |

### `XmlImporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `EnableObjectElementRows` | `true` | Enables importing rows like `<row><name>...</name></row>` |
| `IncludeHeaderRowForObjectElementRows` | `true` | Includes synthesized header row when object-element rows are imported |
| `RowSchemaMode` | `Auto` | `Auto`, `CellsOnly`, or `ObjectElementsOnly` strict schema contract |

### `XmlExporterOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `UseObjectElementRows` | `false` | Exports using first row as header element names and subsequent row values as elements |
| `StrictObjectElementRowWidth` | `false` | When object mode is enabled, enforces each data row width equals header count |

## Adding a New Format

1. Create a class implementing `IDataImporter` and/or `IDataExporter` in the appropriate folder.
2. Set the `SupportedExtension` property (e.g., `".json"`).
3. Register the new type in `ServiceCollectionExtensions.AddDataImportExportManager()`.
4. Add unit tests mirroring existing test class structure.

## Library Design Principles

This is a **class library** intended to be referenced by consuming applications. It follows these principles:

- **Single responsibility** — The library does exactly two things: read a document and return tabular data; receive tabular data and write a document. Orchestration and format detection are the consumer's responsibility.
- **Abstractions only** — Depends on `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`. The consuming application provides the logging pipeline and DI container.
- **Optional loggers** — All constructors accept `ILogger<T>?`; passing `null` (or using no-arg construction) silently falls back to `NullLogger<T>`.
- **No infrastructure opinions** — Does not include telemetry exporters, hosting, or configuration providers.
- **ConfigureAwait(false)** — All `await` calls use `ConfigureAwait(false)` to prevent deadlocks in any consumer `SynchronizationContext`.
- **Configurable resource limits** — `ExcelImporterOptions.MaxBufferSize` lets consumers tune memory limits for their hosting tier.

## Contract Diagnostics

When using `IDataFormatRouter`, contract and routing failures are surfaced with a deterministic diagnostic prefix:

- Format: `[DIXMGR:<extension>:<operation>:<code>] <detail>`
- Example: `[DIXMGR:.json:IMPORT:CONTRACT] JSON import requires a root array of arrays or objects.`

This makes failure handling and telemetry correlation consistent across all supported formats.

Recent diagnostics parity additions include internal machine-readable codes for:

- CSV/TSV delimiter configuration validation (`INVALID_DELIMITER`)
- Excel import validation (`INVALID_WORKBOOK`, `SHEET_NOT_FOUND`, `BUFFER_LIMIT_EXCEEDED`)
- Excel export validation (`INVALID_SHEET_NAME`, `ROW_LIMIT_EXCEEDED`)

### Diagnostics Operation Reference (Current)

| Operation | Meaning |
|-----------|---------|
| `IMPORT` | Operation occurred while reading/importing source content |
| `EXPORT` | Operation occurred while writing/exporting destination content |
| `CONFIG` | Operation occurred while validating configuration/options |

### Diagnostics Code Reference (Current)

| Code | Primary Surface | Meaning |
|------|-----------------|---------|
| `ROUTE_NOT_FOUND` | `IDataFormatRouter` | No importer/exporter is registered for the requested extension |
| `CONTRACT` | `IDataFormatRouter` | Router wrapped a non-prefixed `InvalidOperationException` from a handler |
| `DUPLICATE_HANDLER` | `IDataFormatRouter` | Multiple handlers were registered for the same normalized extension |
| `INVALID_DELIMITER` | CSV/TSV config | Delimiter is reserved (`"`, `\r`, `\n`) |
| `INVALID_ROOT_KIND` | JSON import | Root/record kind not allowed for expected payload shape |
| `INVALID_RECORD_KIND` | JSON/NDJSON import | Record kind is not valid for the expected import shape |
| `MALFORMED_JSON` | JSON/NDJSON import | JSON payload or NDJSON record line cannot be parsed |
| `MIXED_RECORD_TYPES` | JSON/NDJSON import | Mixed array/object record kinds in one logical dataset |
| `BLANK_LINE` | NDJSON import | Blank line encountered while strict blank-line handling is enabled |
| `INVALID_ROOT` | XML import | Root element does not match required schema or XML is malformed |
| `MALFORMED_XML` | XML import | XML payload cannot be parsed |
| `INVALID_ROW_ELEMENT` | XML import | Non-`row` element encountered under `rows` root |
| `SCHEMA_MODE_VIOLATION` | XML import | Row shape violates configured schema mode |
| `OBJECT_ROWS_DISABLED` | XML import | Object-element row mode encountered while disabled |
| `MIXED_ROW_SCHEMAS` | XML import | Mixed `cell` and object-element row schemas detected |
| `INVALID_HEADER` | XML export | Header name empty/invalid for XML element export mode |
| `DUPLICATE_HEADER` | XML export | Duplicate object-element header names detected |
| `ROW_WIDTH_MISMATCH` | XML export | Strict row-width mode failed header/data column alignment |
| `INVALID_WORKBOOK` | Excel import | Workbook part missing from Excel document |
| `SHEET_NOT_FOUND` | Excel import | Requested worksheet could not be resolved |
| `BUFFER_LIMIT_EXCEEDED` | Excel import | Non-seekable input exceeded configured buffering limit |
| `INVALID_SHEET_INDEX` | Excel import | Negative sheet index provided |
| `INVALID_SHEET_NAME` | Excel export config | Empty/whitespace sheet name provided |
| `ROW_LIMIT_EXCEEDED` | Excel export | Data row count exceeded Excel workbook limit |

Diagnostics conformance is validated by a centralized test matrix (`DiagnosticsConformanceTests`) that asserts stable diagnostic message shape across direct handlers and router paths.
Diagnostics metadata is centralized in an internal catalog (`ContractDiagnostics`) to keep operation/code values consistent across handlers, router wrapping, and tests.
`ContractDiagnostics` also provides operation-scoped builder helpers (`BuildImportMessage`, `BuildExportMessage`, `BuildConfigMessage`) to reduce call-site drift.
Unknown diagnostics operations or codes are rejected during message construction to prevent non-catalog values from leaking into runtime telemetry.
Enforcement tests also validate that unknown operation/code values are rejected and that all catalog-defined operations/codes remain recognized.

## Architecture

The solution follows SOLID principles:

- **Single Responsibility** — Each class has one clear purpose (import or export one specific format).
- **Open/Closed** — New formats are added by implementing interfaces, not modifying existing code.
- **Interface Segregation** — Import and export concerns are separated into distinct interfaces.
- **Dependency Inversion** — Services depend on `IDataImporter`/`IDataExporter` abstractions.

## Configuration Files

| File | Purpose |
|------|---------|
| `.editorconfig` | Code style, naming conventions, and analyzer severities |
| `Directory.Build.props` | Shared MSBuild properties across all projects |
| `global.json` | Pins the .NET SDK version |
| `.github/copilot-instructions.md` | GitHub Copilot coding conventions |
| `CONTRIBUTING.md` | Contribution guidelines |
| `SECURITY.md` | Vulnerability reporting policy |
| `LICENSE` | MIT license |

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.


A .NET 10 library for importing and exporting tabular data between CSV and Excel (.xlsx) formats using a clean, extensible architecture.

## Features

- **CSV import/export** — RFC 4180–compliant parsing and writing with quoted field support.
- **Excel import/export** — Reads and writes `.xlsx` files via the [DocumentFormat.OpenXml](https://www.nuget.org/packages/DocumentFormat.OpenXml) SDK.
- **Format conversion pipeline** — Convert between any registered formats through a single `DataPipelineService.ConvertAsync` call.
- **Format detection** — Detect file extensions and query supported formats via `IFileFormatDetector`.
- **Extensible** — Add new formats by implementing `IDataImporter` or `IDataExporter` and registering with the pipeline.
- **Structured logging** — Zero-allocation `[LoggerMessage]` source-generated logging with `ILogger<T>`. The library depends only on logging abstractions — the consuming application configures its own logging pipeline.
- **Configurable options** — `ExcelImporterOptions` allows consumers to tune buffer size limits for their hosting environment.
- **Security hardening** — CSV injection prevention, configurable stream size limits, and resource leak protection.
- **High performance** — `SearchValues<char>` SIMD scanning, `ArrayPool<byte>` buffering, `ValueTask` with pooling, and pre-allocated collections.

## Project Structure

```
DataImportExportManager/
├── Interfaces/                  # Public contracts
│   ├── IDataImporter.cs         # Stream → tabular data
│   ├── IDataExporter.cs         # Tabular data → stream
│   └── IFileFormatDetector.cs   # Extension detection and support queries
├── Importers/                   # IDataImporter implementations
│   ├── CsvImporter.cs
│   ├── ExcelImporter.cs
│   └── ExcelImporterOptions.cs
├── Exporters/                   # IDataExporter implementations
│   ├── CsvExporter.cs
│   └── ExcelExporter.cs
├── Extensions/                  # DI registration
│   └── ServiceCollectionExtensions.cs
└── Services/                    # Orchestration
    ├── DataPipelineService.cs   # Format-to-format conversion
    └── FileFormatDetector.cs    # IFileFormatDetector implementation

DataImportExportManager.Tests/   # xUnit test project
├── CsvImporterTests.cs
├── CsvExporterTests.cs
├── ExcelImporterTests.cs
├── FileFormatDetectorTests.cs
└── DataPipelineServiceTests.cs
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned via `global.json`)

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Usage

### Dependency Injection (Recommended)

Register all services in your DI container:

```csharp
using DataImportExportManager.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Register importers, exporters, pipeline, and detector
builder.Services.AddDataImportExportManager();

// Or configure with custom buffer size for memory-constrained environments
builder.Services.AddDataImportExportManager(excel =>
{
    excel.MaxBufferSize = 25 * 1024 * 1024; // 25 MB for Azure App Service Basic tier
});

var app = builder.Build();
```

Resolve and use the pipeline:

```csharp
var pipeline = app.Services.GetRequiredService<DataPipelineService>();

await using var source = File.OpenRead("data.csv");
await using var destination = File.Create("data.xlsx");

await pipeline.ConvertAsync(source, ".csv", destination, ".xlsx");
```

### Manual Construction

If not using DI, pass `ILogger<T>` instances to each constructor:

```csharp
using Microsoft.Extensions.Logging.Abstractions;

var csvImporter = new CsvImporter(NullLogger<CsvImporter>.Instance);
var excelImporter = new ExcelImporter(NullLogger<ExcelImporter>.Instance, new ExcelImporterOptions());
var csvExporter = new CsvExporter(NullLogger<CsvExporter>.Instance);
var excelExporter = new ExcelExporter(NullLogger<ExcelExporter>.Instance);

IDataImporter[] importers = [csvImporter, excelImporter];
IDataExporter[] exporters = [csvExporter, excelExporter];

var pipeline = new DataPipelineService(importers, exporters, NullLogger<DataPipelineService>.Instance);

await using var source = File.OpenRead("data.csv");
await using var destination = File.Create("data.xlsx");

await pipeline.ConvertAsync(source, ".csv", destination, ".xlsx");
```

### Import CSV Data Directly

```csharp
var importer = new CsvImporter(NullLogger<CsvImporter>.Instance);

await using var stream = File.OpenRead("data.csv");
IReadOnlyList<IReadOnlyList<string>> rows = await importer.ImportAsync(stream);
```

### Detect File Format

```csharp
var detector = new FileFormatDetector(importers, exporters, NullLogger<FileFormatDetector>.Instance);

string extension = detector.DetectExtension("report.xlsx");  // ".xlsx"
bool canImport  = detector.IsImportSupported(extension);      // true
```

## Adding a New Format

1. Create a class implementing `IDataImporter` and/or `IDataExporter` in the appropriate folder.
2. Set the `SupportedExtension` property to the target extension (e.g., `".json"`).
3. Register the new type in `ServiceCollectionExtensions.AddDataImportExportManager()`.
4. Add unit tests mirroring existing test class structure.
5. Update this README if the feature list changes.

## Library Design Principles

This is a **class library** intended to be referenced by consuming applications. It follows these principles:

- **Abstractions only** — Depends on `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`, not their implementation packages. The consuming application provides the logging pipeline and DI container.
- **No infrastructure opinions** — Does not include telemetry exporters, hosting, or configuration providers. The consumer decides where logs go (console, Application Insights, Seq, etc.).
- **ConfigureAwait(false)** — All `await` calls use `ConfigureAwait(false)` to prevent deadlocks in any consumer `SynchronizationContext`.
- **Configurable resource limits** — `ExcelImporterOptions.MaxBufferSize` lets consumers tune memory limits for their hosting tier.

## Architecture

The solution follows SOLID principles:

- **Single Responsibility** — Each class has one clear purpose (import, export, detect, orchestrate).
- **Open/Closed** — New formats are added by implementing interfaces, not modifying existing code.
- **Interface Segregation** — Import and export concerns are separated into distinct interfaces.
- **Dependency Inversion** — Services depend on `IDataImporter`/`IDataExporter` abstractions, not concrete implementations.

## Configuration Files

| File | Purpose |
|------|---------|
| `.editorconfig` | Code style, naming conventions, and analyzer severities |
| `Directory.Build.props` | Shared MSBuild properties across all projects |
| `global.json` | Pins the .NET SDK version |
| `.github/copilot-instructions.md` | GitHub Copilot coding conventions |
| `CONTRIBUTING.md` | Contribution guidelines |
| `SECURITY.md` | Vulnerability reporting policy |
| `LICENSE` | MIT license |

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
