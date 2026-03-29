namespace DataImportExportManager.Exporters;

using System.Diagnostics;
using System.Globalization;
using DataImportExportManager.Interfaces;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exports tabular data to an Excel (.xlsx) stream using the OpenXml SDK.
/// Creates a workbook with a single worksheet whose name is configurable via
/// <see cref="ExcelExporterOptions.SheetName"/>.
/// </summary>
public sealed partial class ExcelExporter : IDataExporter
{
    private readonly ExcelExporterOptions _options;
    private readonly ILogger<ExcelExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelExporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling the exported worksheet name.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public ExcelExporter(ExcelExporterOptions? options = null, ILogger<ExcelExporter>? logger = null)
    {
        _options = options ?? new ExcelExporterOptions();
        _logger = logger ?? NullLogger<ExcelExporter>.Instance;
    }
    /// <inheritdoc />
    public string SupportedExtension => ".xlsx";

    /// <inheritdoc />
    public ValueTask ExportAsync(
        IReadOnlyList<IReadOnlyList<string>> data, Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.SheetName);

        const int ExcelMaxRows = 1_048_576;
        if (data.Count > ExcelMaxRows)
            throw new ArgumentOutOfRangeException(
                nameof(data),
                $"Data contains {data.Count} rows. Excel supports a maximum of {ExcelMaxRows} rows.");
        LogExportStarted(data.Count);
        var startTimestamp = Stopwatch.GetTimestamp();

        using var document = SpreadsheetDocument.Create(destination, SpreadsheetDocumentType.Workbook);

        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = _options.SheetName
        });

        uint rowIndex = 1;
        foreach (var rowData in data)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new Row { RowIndex = rowIndex };

            for (int colIndex = 0; colIndex < rowData.Count; colIndex++)
            {
                var cell = new Cell
                {
                    CellReference = GetCellReference(colIndex, rowIndex),
                    DataType = CellValues.String,
                    CellValue = new CellValue(rowData[colIndex] ?? string.Empty)
                };
                row.Append(cell);
            }

            sheetData.Append(row);
            rowIndex++;
        }

        workbookPart.Workbook.Save();
        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogExportCompleted(data.Count, elapsedMs);
        return default;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Excel export started with {RowCount} rows")]
    private partial void LogExportStarted(int rowCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Excel export completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogExportCompleted(int rowCount, double elapsedMs);

    /// <summary>
    /// Converts a zero-based column index and one-based row index to an Excel cell reference
    /// (e.g. column 0, row 1 → "A1"; column 25, row 2 → "Z2"; column 26, row 3 → "AA3").
    /// Uses stackalloc to avoid heap allocation for the column letter portion.
    /// </summary>
    private static string GetCellReference(int zeroBasedColumnIndex, uint rowIndex)
    {
        // Excel columns are base-26 bijective (A=0, Z=25, AA=26 …).
        // Maximum column in .xlsx is XFD (index 16383) → at most 3 letters.
        Span<char> letters = stackalloc char[3];
        int pos = letters.Length;
        int col = zeroBasedColumnIndex;
        do
        {
            letters[--pos] = (char)('A' + col % 26);
            col = col / 26 - 1;
        }
        while (col >= 0);
        return string.Concat(letters[pos..], rowIndex.ToString(CultureInfo.InvariantCulture));
    }
}
