namespace DataImportExportManager.Importers;

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using DataImportExportManager.Diagnostics;
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
            ?? throw new InvalidOperationException(
                ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.InvalidWorkbook, "The Excel document has no workbook."));

        if (options.SheetIndex is int sheetIndex)
        {
            if (sheetIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.InvalidSheetIndex, "SheetIndex cannot be negative."));
            }
        }

        var allSheets = workbookPart.Workbook.Descendants<Sheet>();
        var sheet = options.SheetName is not null
            ? allSheets.FirstOrDefault(s => s.Name?.Value?.Equals(options.SheetName, StringComparison.OrdinalIgnoreCase) == true)
                ?? throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.SheetNotFound, $"No worksheet named '{options.SheetName}' was found."))
            : options.SheetIndex is not null
                ? allSheets.ElementAtOrDefault(options.SheetIndex.Value)
                    ?? throw new InvalidOperationException(
                        ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.SheetNotFound, $"No worksheet at index {options.SheetIndex} was found."))
                : allSheets.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.SheetNotFound, "The Excel document has no sheets."));

        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

        if (sheetData is null)
        {
            return [];
        }

        // Cache shared strings in a list for O(1) lookup instead of O(n) ElementAt
        var sharedStrings = CacheSharedStrings(workbookPart);
        var workbookUses1904DateSystem = workbookPart.Workbook.WorkbookProperties?.Date1904?.Value == true;
        var results = new List<IReadOnlyList<string>>();
        var columnHint = 0;

        foreach (var row in sheetData.Elements<Row>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowData = new List<string>(columnHint);
            foreach (var cell in row.Elements<Cell>())
            {
                // Excel omits cells that have no value, so a row with data in A and C
                // only has two Cell elements in the XML. Use CellReference to detect
                // the gap and backfill with empty strings to keep column alignment.
                if (cell.CellReference?.Value is string cellRef)
                {
                    var expectedIndex = ParseColumnIndex(cellRef);
                    while (rowData.Count < expectedIndex)
                    {
                        rowData.Add(string.Empty);
                    }
                }

                rowData.Add(GetCellValue(cell, sharedStrings, options, workbookPart, workbookUses1904DateSystem));
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
                        ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.BufferLimitExceeded, $"The source stream exceeds the maximum allowed size of {maxBytes / (1024 * 1024)} MB."));
                }

                destination.Write(rentedBuffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Converts the column-letter prefix of an Excel cell reference (e.g., "C" from "C7",
    /// "AA" from "AA3") to a zero-based column index using bijective base-26 arithmetic.
    /// </summary>
    private static int ParseColumnIndex(ReadOnlySpan<char> cellReference)
    {
        int col = 0;
        foreach (char c in cellReference)
        {
            if (!char.IsAsciiLetter(c)) break;

            if (col > (int.MaxValue - 26) / 26)
            {
                return 0;
            }

            col = col * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return col == 0 ? 0 : col - 1;
    }

    private static string GetCellValue(
        Cell cell,
        List<string>? sharedStrings,
        ExcelImporterOptions options,
        WorkbookPart workbookPart,
        bool workbookUses1904DateSystem)
    {
        string value;

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            value = cell.InlineString?.InnerText ?? string.Empty;
        }
        else if (cell.CellValue is null)
        {
            return string.Empty;
        }

        else if (cell.DataType?.Value == CellValues.SharedString
            && sharedStrings is not null
            && int.TryParse(cell.CellValue.InnerText, out int index)
            && index >= 0 && index < sharedStrings.Count)
        {
            value = sharedStrings[index];
        }
        else
        {
            value = cell.CellValue.InnerText;
        }

        value = value.Trim();

        if (options.ParseDateFormattedCells
            && TryConvertDateFormattedNumericCell(cell, value, workbookPart, workbookUses1904DateSystem, out var parsedDateValue))
        {
            value = parsedDateValue;
        }

        if (options.NormalizeHiddenCharacters)
        {
            value = NormalizeHiddenCharacters(value).Trim();
        }

        return value;
    }

    private static bool TryConvertDateFormattedNumericCell(
        Cell cell,
        string value,
        WorkbookPart workbookPart,
        bool workbookUses1904DateSystem,
        out string converted)
    {
        converted = string.Empty;

        if (cell.CellFormula is not null)
        {
            return false;
        }

        var dataType = cell.DataType?.Value;
        if (dataType == CellValues.SharedString
            || dataType == CellValues.String
            || dataType == CellValues.InlineString
            || dataType == CellValues.Boolean)
        {
            return false;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            return false;
        }

        if (!IsDateFormattedCell(cell, workbookPart, serial))
        {
            return false;
        }

        if (workbookUses1904DateSystem)
        {
            serial += 1462d;
        }

        try
        {
            var dateTime = DateTime.FromOADate(serial);
            converted = dateTime.ToString("O", CultureInfo.InvariantCulture);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsDateFormattedCell(Cell cell, WorkbookPart workbookPart, double numericValue)
    {
        var styleIndex = (int?)cell.StyleIndex?.Value;
        if (styleIndex is null)
        {
            return false;
        }

        var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
        var cellFormats = stylesheet?.CellFormats;
        if (cellFormats is null)
        {
            return false;
        }

        var format = cellFormats.Elements<CellFormat>().ElementAtOrDefault(styleIndex.Value);
        if (format is null)
        {
            return false;
        }

        var numberFormatId = format.NumberFormatId?.Value ?? 0;
        if (IsBuiltInDateFormat(numberFormatId))
        {
            return true;
        }

        var customFormatCode = stylesheet?.NumberingFormats?
            .Elements<NumberingFormat>()
            .FirstOrDefault(n => n.NumberFormatId?.Value == numberFormatId)?
            .FormatCode?.Value;

        if (customFormatCode is null)
        {
            return false;
        }

        var applicableSection = GetApplicableNumericFormatSection(customFormatCode, numericValue);
        return ContainsDateFormatTokens(applicableSection);
    }

    private static bool IsBuiltInDateFormat(uint numberFormatId)
        => numberFormatId is >= 14 and <= 22
            or >= 27 and <= 36
            or >= 45 and <= 47
            or >= 50 and <= 58;

    private static bool ContainsDateFormatTokens(string formatCode)
    {
        bool inQuotedLiteral = false;

        for (int i = 0; i < formatCode.Length; i++)
        {
            var c = formatCode[i];

            if (inQuotedLiteral)
            {
                if (c == '"')
                {
                    if (i + 1 < formatCode.Length && formatCode[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inQuotedLiteral = false;
                }

                continue;
            }

            if (c == '"')
            {
                inQuotedLiteral = true;
                continue;
            }

            if (c is '\\' or '_' or '*')
            {
                i++;
                continue;
            }

            if (c == '[')
            {
                int closingBracketIndex = formatCode.IndexOf(']', i + 1);
                if (closingBracketIndex < 0)
                {
                    break;
                }

                if (ContainsElapsedTimeToken(formatCode.AsSpan(i + 1, closingBracketIndex - i - 1)))
                {
                    return true;
                }

                i = closingBracketIndex;
                continue;
            }

            if (IsDateOrTimeToken(c))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetApplicableNumericFormatSection(string formatCode, double numericValue)
    {
        var sections = SplitFormatSections(formatCode);
        if (sections.Count == 0)
        {
            return formatCode;
        }

        int numericSectionCount = sections.Count >= 4 ? 3 : sections.Count;
        var numericSections = sections.Take(numericSectionCount).ToList();
        if (numericSections.Count == 0)
        {
            return formatCode;
        }

        bool hasAnyCondition = numericSections.Any(HasCondition);
        if (hasAnyCondition)
        {
            string? fallbackSection = null;
            foreach (var section in numericSections)
            {
                if (TryEvaluateSectionCondition(section, numericValue, out var matches))
                {
                    if (matches)
                    {
                        return section;
                    }

                    continue;
                }

                fallbackSection ??= section;
            }

            return fallbackSection ?? numericSections[^1];
        }

        return numericSections.Count switch
        {
            1 => numericSections[0],
            2 => numericValue < 0 ? numericSections[1] : numericSections[0],
            _ => numericValue > 0
                ? numericSections[0]
                : numericValue < 0
                    ? numericSections[1]
                    : numericSections[2],
        };
    }

    private static List<string> SplitFormatSections(string formatCode)
    {
        var sections = new List<string>();
        int sectionStart = 0;
        bool inQuotedLiteral = false;
        int bracketDepth = 0;

        for (int i = 0; i < formatCode.Length; i++)
        {
            var c = formatCode[i];

            if (inQuotedLiteral)
            {
                if (c == '"')
                {
                    if (i + 1 < formatCode.Length && formatCode[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inQuotedLiteral = false;
                }

                continue;
            }

            if (c == '"')
            {
                inQuotedLiteral = true;
                continue;
            }

            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c == '[')
            {
                bracketDepth++;
                continue;
            }

            if (c == ']' && bracketDepth > 0)
            {
                bracketDepth--;
                continue;
            }

            if (c == ';' && bracketDepth == 0)
            {
                sections.Add(formatCode.Substring(sectionStart, i - sectionStart));
                sectionStart = i + 1;
            }
        }

        sections.Add(formatCode[sectionStart..]);
        return sections;
    }

    private static bool HasCondition(string section)
        => TryEvaluateSectionCondition(section, 0d, out _);

    private static bool TryEvaluateSectionCondition(string section, double numericValue, out bool isMatch)
    {
        isMatch = false;
        int index = 0;

        while (index < section.Length && section[index] == '[')
        {
            int closing = section.IndexOf(']', index + 1);
            if (closing < 0)
            {
                break;
            }

            var bracket = section.Substring(index + 1, closing - index - 1);
            if (TryParseCondition(bracket, out var op, out var threshold))
            {
                isMatch = EvaluateCondition(op, numericValue, threshold);
                return true;
            }

            index = closing + 1;
        }

        return false;
    }

    private static bool TryParseCondition(string bracketContent, out string op, out double threshold)
    {
        op = string.Empty;
        threshold = 0d;

        ReadOnlySpan<char> text = bracketContent.AsSpan().Trim();
        if (text.Length < 2)
        {
            return false;
        }

        if (text.StartsWith(">="))
        {
            op = ">=";
            text = text[2..];
        }
        else if (text.StartsWith("<="))
        {
            op = "<=";
            text = text[2..];
        }
        else if (text.StartsWith("<>"))
        {
            op = "<>";
            text = text[2..];
        }
        else if (text[0] is '>' or '<' or '=')
        {
            op = text[0].ToString();
            text = text[1..];
        }
        else
        {
            return false;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold);
    }

    private static bool EvaluateCondition(string op, double value, double threshold)
        => op switch
        {
            ">=" => value >= threshold,
            "<=" => value <= threshold,
            ">" => value > threshold,
            "<" => value < threshold,
            "=" => value == threshold,
            "<>" => value != threshold,
            _ => false,
        };

    private static bool IsDateOrTimeToken(char c)
    {
        var normalized = char.ToLowerInvariant(c);
        return normalized is 'y' or 'm' or 'd' or 'h' or 's';
    }

    private static bool ContainsElapsedTimeToken(ReadOnlySpan<char> bracketContent)
    {
        if (bracketContent.Length == 0 || bracketContent[0] == '$')
        {
            return false;
        }

        char token = char.ToLowerInvariant(bracketContent[0]);
        if (token is not ('h' or 'm' or 's'))
        {
            return false;
        }

        foreach (var c in bracketContent)
        {
            if (char.ToLowerInvariant(c) != token)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeHiddenCharacters(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c == '\u00A0')
            {
                builder.Append(' ');
                continue;
            }

            if (char.IsControl(c) && c is not '\t' and not '\r' and not '\n')
            {
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Excel import started")]
    private partial void LogImportStarted();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Buffering non-seekable stream into memory (max {MaxSize} bytes)")]
    private partial void LogBufferingNonSeekableStream(long maxSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Excel import completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogImportCompleted(int rowCount, double elapsedMs);

    private const string SupportedExtensionValue = ".xlsx";
}
