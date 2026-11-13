# DataImportExportManager configuration reference

Every option of this package, its default, and whether it is required or secret. The host app supplies the
values in code, through the `configure...` callbacks of `AddDataImportExportManager` and
`AddDistributedImportSchemaSessionCache`. The package reads no configuration file and no environment variable,
so where the host keeps the values is the host's choice. A test keeps this file in step with the code, so a
default listed here is the default in the package.

No option of this package is required, and none is a secret. The package connects to no service of its own. The
session cache uses whichever `IDistributedCache` the host registers, and the connection to that cache belongs to
the host.

| Column | Meaning |
|---|---|
| Default | The value used when the host sets nothing. `none` means the option has no value until the host sets one. An encoding is shown by its web name. |
| Required | `Yes` means the host must supply a value. `Conditional` means it is required under a stated condition. `No` means it can be left alone. |
| Secret | `Yes` means the value is a secret. It comes from Key Vault when the app is deployed and from user secrets in local development. It never goes in a configuration file. |

## Overriding a default

```csharp
builder.Services.AddDataImportExportManager(
    configureCsvImporter: options => options.Delimiter = '\t',
    configureCsvExporter: options => options.SanitizeFormulaCells = true,
    configureExcelImporter: options => options.MaxBufferSize = 25 * 1024 * 1024);

builder.Services.AddDistributedImportSchemaSessionCache(
    defaultTtl: TimeSpan.FromMinutes(10),
    configureOptions: options => options.KeyPrefix = "orders-import");
```

## Session time to live

The session cache time to live is a parameter of `AddImportSchemaSessionCache` and
`AddDistributedImportSchemaSessionCache`, not an option. It defaults to 20 minutes and must be above zero. A
caller can also pass a time to live on each call, and that value wins.

## `CsvImporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `Encoding` | `utf-8` | No | No | Text encoding used to read the stream |
| `Delimiter` | `,` | No | No | Character that separates fields |

## `CsvExporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `Encoding` | `utf-8` | No | No | Text encoding used to write the stream. The default is UTF-8 without a byte-order mark |
| `Delimiter` | `,` | No | No | Character that separates fields |
| `SanitizeFormulaCells` | `false` | No | No | Prefixes a cell that starts with `=`, `+`, `-`, `@`, tab or carriage return with a single quote, which prevents spreadsheet formula injection. Turn it on when the file may be opened in a spreadsheet and the data can hold user-supplied values |

## `ExcelImporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `MaxBufferSize` | `104857600` | No | No | Most bytes (100 MB) buffered into memory when the source stream cannot seek. A larger stream is rejected. Lower it where memory is tight or many imports run at once |
| `SheetName` | `none` | No | No | Worksheet to import, matched without regard to case. It wins over `SheetIndex` when both are set. With neither set, the first worksheet is imported |
| `SheetIndex` | `none` | No | No | Zero-based index of the worksheet to import. It cannot be negative |
| `NormalizeHiddenCharacters` | `false` | No | No | Removes zero-width format characters and control characters other than tab and newline, and turns non-breaking spaces into plain spaces. Off keeps the raw imported values |
| `ParseDateFormattedCells` | `false` | No | No | Returns numeric cells that carry a date format as ISO 8601 date and time text. Off returns the number as stored |

## `ExcelExporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `SheetName` | `Sheet1` | No | No | Name of the worksheet created in the exported workbook. It cannot be blank |

## `JsonImporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `IncludeHeaderRowForObjectRecords` | `true` | No | No | For an array of JSON objects, adds a generated header row as the first result row |

## `JsonExporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `WriteIndented` | `false` | No | No | Writes indented JSON. Off writes compact JSON |

## `NdjsonImporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `IgnoreBlankLines` | `true` | No | No | Skips blank and whitespace-only lines. Off makes a blank line an error |

## `NdjsonExporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `WriteTrailingNewline` | `true` | No | No | Writes a final newline after the last record |

## `XmlImporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `RowSchemaMode` | `Auto` | No | No | `Auto` detects the row shape but rejects a document that mixes shapes. `CellsOnly` requires every row to use `<cell>` elements. `ObjectElementsOnly` requires every row to use object elements such as `<name>` |
| `EnableObjectElementRows` | `true` | No | No | Imports rows shaped like `<row><name>Ada</name></row>` as a header row and value rows |
| `IncludeHeaderRowForObjectElementRows` | `true` | No | No | Adds the generated header row when object-element rows are imported |

## `XmlExporterOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `UseObjectElementRows` | `false` | No | No | Treats the first row as header names and writes later rows as `<row><header>value</header></row>` |
| `StrictObjectElementRowWidth` | `false` | No | No | With `UseObjectElementRows` on, requires every data row to have as many columns as the header row |

## `DistributedImportSchemaSessionCacheOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `KeyPrefix` | `dixmgr-sess` | No | No | Prefix of every session key in the distributed cache. It cannot be blank. Give each application its own prefix when they share a cache |
| `MaxPayloadBytes` | `1048576` | No | No | Largest serialized session, in bytes, the cache accepts. It must be above 0 |
