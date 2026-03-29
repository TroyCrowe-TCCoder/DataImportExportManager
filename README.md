# DataImportExportManager

A .NET 10 library for importing and exporting tabular data between CSV and Excel (.xlsx) formats using a clean, extensible architecture.

## Features

- **CSV import/export** — RFC 4180–compliant parsing and writing with full support for multi-line quoted fields, configurable delimiter, and configurable encoding.
- **Excel import/export** — Reads and writes `.xlsx` files via the [DocumentFormat.OpenXml](https://www.nuget.org/packages/DocumentFormat.OpenXml) SDK with configurable sheet selection.
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
│   └── IDataExporter.cs         # Tabular data → stream
├── Importers/                   # IDataImporter implementations
│   ├── CsvImporter.cs
│   ├── CsvImporterOptions.cs
│   ├── ExcelImporter.cs
│   └── ExcelImporterOptions.cs
├── Exporters/                   # IDataExporter implementations
│   ├── CsvExporter.cs
│   ├── CsvExporterOptions.cs
│   └── ExcelExporter.cs
└── Extensions/                  # DI registration
    └── ServiceCollectionExtensions.cs

DataImportExportManager.Tests/   # xUnit test project
├── CsvImporterTests.cs
├── CsvExporterTests.cs
└── ExcelImporterTests.cs
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
    });
```

Resolve and use importers/exporters directly — composing them is the consumer's responsibility:

```csharp
// Inject IEnumerable<IDataImporter> and IEnumerable<IDataExporter>
var csvImporter  = importers.First(i => i.SupportedExtension == ".csv");
var xlsxExporter = exporters.First(e => e.SupportedExtension == ".xlsx");

await using var source      = File.OpenRead("data.csv");
await using var destination = File.Create("data.xlsx");

var rows = await csvImporter.ImportAsync(source);
await xlsxExporter.ExportAsync(rows, destination);
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
