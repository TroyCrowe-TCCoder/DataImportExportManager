namespace DataImportExportManager.Importers;

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DataImportExportManager.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Imports tabular data from an Excel (.xlsx) stream using the OpenXml SDK.
/// Reads the worksheet specified by <see cref="ExcelImporterOptions.SheetName"/> or
/// <see cref="ExcelImporterOptions.SheetIndex"/> (defaulting to the first worksheet)
/// and returns all rows including headers.
/// Non-seekable streams are buffered into memory automatically.
/// </summary>
public sealed partial class ExcelImporter : IDataImporter
{
    private readonly ExcelImporterOptions _options;
    private readonly ILogger<ExcelImporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelImporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling buffer size limits and sheet selection.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public ExcelImporter(ExcelImporterOptions? options = null, ILogger<ExcelImporter>? logger = null)
    {
        _options = options ?? new ExcelImporterOptions();
        _logger = logger ?? NullLogger<ExcelImporter>.Instance;
    }

    /// <inheritdoc />
    public string SupportedExtension => ".xlsx";

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        LogImportStarted();
        var startTimestamp = Stopwatch.GetTimestamp();

        // OpenXml requires a seekable stream; buffer into a MemoryStream if needed.
        // The try/finally wraps the buffering step so the MemoryStream is disposed
        // even when CopyWithSizeLimitAsync rejects an oversized or cancelled stream.
        var seekableStream = source;
        MemoryStream? buffer = null;

        try
        {
            if (!source.CanSeek)
            {
                LogBufferingNonSeekableStream(_options.MaxBufferSize);
                buffer = new MemoryStream();
                await CopyWithSizeLimitAsync(source, buffer, _options.MaxBufferSize, cancellationToken).ConfigureAwait(false);
                buffer.Position = 0;
                seekableStream = buffer;
            }

            using var document = SpreadsheetDocument.Open(seekableStream, false);
            var results = ReadAllRows(document, _options, cancellationToken);
            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            LogImportCompleted(results.Count, elapsedMs);
            return results;
        }
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static List<IReadOnlyList<string>> ReadAllRows(
        SpreadsheetDocument document, ExcelImporterOptions options, CancellationToken cancellationToken)
    {
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidOperationException("The Excel document has no workbook.");

        var allSheets = workbookPart.Workbook.Descendants<Sheet>();
        var sheet = options.SheetName is not null
            ? allSheets.FirstOrDefault(s => s.Name?.Value?.Equals(options.SheetName, StringComparison.OrdinalIgnoreCase) == true)
                ?? throw new InvalidOperationException($"No worksheet named '{options.SheetName}' was found.")
            : options.SheetIndex is not null
                ? allSheets.ElementAtOrDefault(options.SheetIndex.Value)
                    ?? throw new InvalidOperationException($"No worksheet at index {options.SheetIndex} was found.")
                : allSheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("The Excel document has no sheets.");

        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

        if (sheetData is null)
        {
            return [];
        }

        // Cache shared strings in a list for O(1) lookup instead of O(n) ElementAt
        var sharedStrings = CacheSharedStrings(workbookPart);
        var results = new List<IReadOnlyList<string>>();
        var columnHint = 0;

        foreach (var row in sheetData.Elements<Row>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowData = new List<string>(columnHint);
            foreach (var cell in row.Elements<Cell>())
            {
                rowData.Add(GetCellValue(cell, sharedStrings));
            }

            if (columnHint == 0)
            {
                columnHint = rowData.Count;
            }

            results.Add(rowData);
        }

        return results;
    }

    private static List<string>? CacheSharedStrings(WorkbookPart workbookPart)
    {
        var table = workbookPart.SharedStringTablePart?.SharedStringTable;
        if (table is null)
        {
            return null;
        }

        var capacity = (int?)table.Count?.Value ?? 0;
        var cached = new List<string>(capacity);
        foreach (var element in table.Elements())
        {
            cached.Add(element.InnerText);
        }
        return cached;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="destination"/> in chunks,
    /// throwing if the accumulated bytes exceed <paramref name="maxBytes"/>.
    /// Uses <see cref="ArrayPool{T}"/> to avoid per-call buffer allocations.
    /// </summary>
    private static async Task CopyWithSizeLimitAsync(
        Stream source, MemoryStream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(rentedBuffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (destination.Length + bytesRead > maxBytes)
                {
                    throw new InvalidOperationException(
                        $"The source stream exceeds the maximum allowed size of {maxBytes / (1024 * 1024)} MB.");
                }

                destination.Write(rentedBuffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static string GetCellValue(Cell cell, List<string>? sharedStrings)
    {
        if (cell.CellValue is null)
        {
            return string.Empty;
        }

        var value = cell.CellValue.InnerText;

        if (cell.DataType?.Value == CellValues.SharedString
            && sharedStrings is not null
            && int.TryParse(value, out int index)
            && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        return value;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Excel import started")]
    private partial void LogImportStarted();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Buffering non-seekable stream into memory (max {MaxSize} bytes)")]
    private partial void LogBufferingNonSeekableStream(long maxSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Excel import completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogImportCompleted(int rowCount, double elapsedMs);
}
