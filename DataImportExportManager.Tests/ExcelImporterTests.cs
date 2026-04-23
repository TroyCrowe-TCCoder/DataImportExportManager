namespace DataImportExportManager.Tests;

using System.Globalization;
using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

/// <summary>
/// Unit tests for <see cref="ExcelImporter"/>, validating null guards,
/// extension metadata, sheet selection, and size-limited buffering.
/// </summary>
public class ExcelImporterTests
{
    private readonly ExcelImporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsXlsx()
    {
        Assert.Equal(".xlsx", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenSourceIsNullThenThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ImportAsync(null!).AsTask());
    }

    [Fact]
    public async Task WhenNonSeekableStreamExceedsMaxSizeThenThrowsDuringCopy()
    {
        // The internal MaxBufferSize is 100 MB. A non-seekable stream that yields
        // more than 100 MB must be rejected with InvalidOperationException before
        // the entire payload is buffered — proving the size check is enforced
        // during the copy, not after.
        using var oversizedStream = new NonSeekableRepeatingStream(
            byteValue: 0xFF, totalLength: (100 * 1024 * 1024) + 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(oversizedStream).AsTask());

        Assert.Contains("exceeds the maximum allowed size", ex.Message);
        Assert.Contains(":BUFFER_LIMIT_EXCEEDED]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenCancellationRequestedThenThrowsOperationCanceledException()
    {
        // Create a valid .xlsx so OpenXml doesn't throw FileFormatException first
        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [["A"], ["1"]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ImportAsync(xlsxStream, cts.Token).AsTask());
    }

    [Fact]
    public async Task WhenNonSeekableStreamCancelledDuringCopyThenThrowsOperationCanceledException()
    {
        // A non-seekable stream with a pre-cancelled token exercises the path
        // where CopyWithSizeLimitAsync throws inside the buffering block.
        // Before the try/finally restructure, this path leaked the MemoryStream.
        using var infiniteStream = new NonSeekableRepeatingStream(
            byteValue: 0xAA, totalLength: long.MaxValue);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ImportAsync(infiniteStream, cts.Token).AsTask());
    }

    [Fact]
    public async Task WhenSheetNameNotFoundThenThrowsInvalidOperationException()
    {
        // Create a valid .xlsx with the default "Sheet1" and request a non-existent sheet.
        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [["A"]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        var sut = new ExcelImporter(new ExcelImporterOptions { SheetName = "DoesNotExist" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ImportAsync(xlsxStream).AsTask());

        Assert.Contains("DoesNotExist", ex.Message);
        Assert.Contains(":SHEET_NOT_FOUND]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenSheetIndexOutOfRangeThenThrowsInvalidOperationException()
    {
        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [["A"]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        var sut = new ExcelImporter(new ExcelImporterOptions { SheetIndex = 99 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ImportAsync(xlsxStream).AsTask());

        Assert.Contains("99", ex.Message);
        Assert.Contains(":SHEET_NOT_FOUND]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenSheetIndexNegativeThenThrowsArgumentOutOfRangeException()
    {
        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [["A"]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        var sut = new ExcelImporter(new ExcelImporterOptions { SheetIndex = -1 });

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.ImportAsync(xlsxStream).AsTask());

        Assert.Contains(":INVALID_SHEET_INDEX]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenSheetNameAndSheetIndexBothSetThenSheetNameTakesPrecedence()
    {
        using var xlsxStream = BuildWorkbookWithSheets([
            ("First", [["FromFirst"]]),
            ("Second", [["FromSecond"]]),
        ]);

        var sut = new ExcelImporter(new ExcelImporterOptions
        {
            SheetName = "Second",
            SheetIndex = 0,
        });

        var result = await sut.ImportAsync(xlsxStream);

        Assert.Single(result);
        Assert.Equal(["FromSecond"], result[0]);
    }

    [Fact]
    public async Task WhenCellsHaveLeadingOrTrailingSpacesThenTrimsWhitespace()
    {
        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [[" Name ", " Age "], [" John ", " 30 "]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Equal(2, result.Count);
        Assert.Equal(["Name", "Age"], result[0]);
        Assert.Equal(["John", "30"], result[1]);
    }

    [Fact]
    public async Task WhenRowHasSparseColumnsThenPadsWithEmptyStrings()
    {
        // Simulate a file produced outside this library where empty cells are
        // simply absent from the XML rather than written as empty Cell elements.
        // Row 1: A1="Header1", C1="Header3" — B1 is missing (gap at middle)
        // Row 2: B2="Value2"               — A2 and C2 are missing (gaps at start and end)
        using var xlsxStream = BuildSparseXlsx([
            [("A1", "Header1"), ("C1", "Header3")],
            [("B2", "Value2")],
        ]);

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Equal(2, result.Count);
        Assert.Equal(["Header1", "", "Header3"], result[0]);
        Assert.Equal(["", "Value2"], result[1]);
    }

    [Fact]
    public async Task WhenCellReferenceUsesLowercaseColumnThenPadsSparseColumnsCorrectly()
    {
        using var xlsxStream = BuildSparseXlsx([
            [("a1", "Header1"), ("c1", "Header3")],
        ]);

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Single(result);
        Assert.Equal(["Header1", "", "Header3"], result[0]);
    }

    [Fact]
    public async Task WhenCellReferenceHasNoColumnPrefixThenTreatsAsFirstColumn()
    {
        using var xlsxStream = BuildSparseXlsx([
            [("1", "Header1"), ("C1", "Header3")],
        ]);

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Single(result);
        Assert.Equal(["Header1", "", "Header3"], result[0]);
    }

    [Theory]
    [InlineData("O'Neil")]
    [InlineData("ACME-01")]
    public async Task WhenValueContainsRequiredSpecialCharactersThenPreservesValue(string expected)
    {
        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [[expected]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Equal(expected, result[0][0]);
    }

    [Fact]
    public async Task WhenCellUsesInlineStringThenImportsInlineText()
    {
        using var xlsxStream = BuildInlineStringXlsx("A1", "Inline Text");

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Single(result);
        Assert.Equal(["Inline Text"], result[0]);
    }

    [Fact]
    public async Task WhenHiddenCharacterNormalizationDisabledThenPreservesHiddenCharacters()
    {
        const string expected = "A\u200BB\u00A0C";

        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [[expected]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Equal(expected, result[0][0]);
    }

    [Fact]
    public async Task WhenHiddenCharacterNormalizationEnabledThenRemovesHiddenCharacters()
    {
        const string raw = "A\u200BB\u00A0C";

        using var xlsxStream = new MemoryStream();
        var exporter = new ExcelExporter();
        List<IReadOnlyList<string>> data = [[raw]];
        await exporter.ExportAsync(data, xlsxStream);
        xlsxStream.Position = 0;

        var sut = new ExcelImporter(new ExcelImporterOptions { NormalizeHiddenCharacters = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal("AB C", result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingDisabledThenReturnsRawDateSerial()
    {
        var date = new DateTime(2024, 01, 15);
        double serial = date.ToOADate();

        using var xlsxStream = BuildDateFormattedXlsx(serial);

        var result = await _sut.ImportAsync(xlsxStream);

        Assert.Equal(serial.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledThenReturnsIso8601DateTime()
    {
        var date = new DateTime(2024, 01, 15);
        double serial = date.ToOADate();

        using var xlsxStream = BuildDateFormattedXlsx(serial);

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(date.ToString("O", CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndScientificConditionDateSectionDoesNotApplyThenReturnsRawValue()
    {
        const double numericValue = 200d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "[>=1E3]yyyy-mm-dd;0");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndScientificConditionDateSectionAppliesThenReturnsIso8601DateTime()
    {
        var date = new DateTime(2024, 01, 15);
        double serial = date.ToOADate();
        using var xlsxStream = BuildCustomFormattedNumericXlsx(serial, "[>=1E3]yyyy-mm-dd;0");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(date.ToString("O", CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomFormatUsesEscapedDateLettersThenReturnsRawValue()
    {
        const double numericValue = 45292d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "0 \\d\\a\\y\\s");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomFormatHasDateTokenOnlyInTextSectionThenReturnsRawValue()
    {
        const double numericValue = 123d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "0;0;0;yyyy-mm-dd");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomFormatHasTextSectionWithEscapedDateLettersThenReturnsRawValue()
    {
        const double numericValue = 123d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "0;0;0;\\d\\a\\y");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndConditionalDateSectionDoesNotApplyThenReturnsRawValue()
    {
        const double numericValue = 200d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "[>=100]0;yyyy-mm-dd");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndConditionalDateSectionAppliesThenReturnsIso8601DateTime()
    {
        var date = new DateTime(2024, 01, 15);
        double serial = date.ToOADate();
        using var xlsxStream = BuildCustomFormattedNumericXlsx(serial, "[>=50000]0;yyyy-mm-dd");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(date.ToString("O", CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomFormatUsesColorSectionThenReturnsRawValue()
    {
        const double numericValue = 45292d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "[Red]0");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomFormatUsesConditionSectionThenReturnsRawValue()
    {
        const double numericValue = 45292d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "[>=100]0");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomFormatUsesLocaleBracketThenReturnsRawValue()
    {
        const double numericValue = 45292d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "[$-409]0");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomFormatHasDateLettersInQuotedLiteralThenReturnsRawValue()
    {
        const double numericValue = 45292d;
        using var xlsxStream = BuildCustomFormattedNumericXlsx(numericValue, "0 \"days\"");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(numericValue.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCustomDateFormatThenReturnsIso8601DateTime()
    {
        var date = new DateTime(2024, 01, 15);
        double serial = date.ToOADate();
        using var xlsxStream = BuildCustomFormattedNumericXlsx(serial, "yyyy-mm-dd");

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(date.ToString("O", CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndWorkbookUses1904SystemThenConvertsUsing1904Base()
    {
        using var xlsxStream = BuildDateFormattedXlsx(dateSerial: 0d, uses1904DateSystem: true);

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(new DateTime(1904, 01, 01).ToString("O", CultureInfo.InvariantCulture), result[0][0]);
    }

    [Fact]
    public async Task WhenDateParsingEnabledAndCellContainsFormulaThenPreservesCachedRawValue()
    {
        const double cachedSerial = 45292d;
        using var xlsxStream = BuildDateFormulaCellXlsx(cachedSerial);

        var sut = new ExcelImporter(new ExcelImporterOptions { ParseDateFormattedCells = true });
        var result = await sut.ImportAsync(xlsxStream);

        Assert.Equal(cachedSerial.ToString(CultureInfo.InvariantCulture), result[0][0]);
    }

    /// <summary>
    /// Builds a minimal xlsx stream where only the supplied cells are written,
    /// leaving all other column positions absent from the XML.
    /// </summary>
    private static MemoryStream BuildSparseXlsx(IEnumerable<IEnumerable<(string Reference, string Value)>> rows)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            uint rowIndex = 1;
            foreach (var rowCells in rows)
            {
                var row = new Row { RowIndex = rowIndex++ };
                foreach (var (reference, value) in rowCells)
                {
                    row.Append(new Cell
                    {
                        CellReference = reference,
                        DataType = CellValues.String,
                        CellValue = new CellValue(value)
                    });
                }
                sheetData.Append(row);
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildCustomFormattedNumericXlsx(double value, string formatCode)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            const uint customFormatId = 164;
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(
                new NumberingFormats(new NumberingFormat
                {
                    NumberFormatId = customFormatId,
                    FormatCode = formatCode,
                }),
                new Fonts(new Font()),
                new Fills(new Fill()),
                new Borders(new Border()),
                new CellStyleFormats(new CellFormat()),
                new CellFormats(
                    new CellFormat(),
                    new CellFormat
                    {
                        NumberFormatId = customFormatId,
                        ApplyNumberFormat = true,
                    }));

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var row = new Row { RowIndex = 1 };
            row.Append(new Cell
            {
                CellReference = "A1",
                StyleIndex = 1,
                CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
            });
            sheetData.Append(row);
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildDateFormattedXlsx(double dateSerial, bool uses1904DateSystem = false)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            workbookPart.Workbook.WorkbookProperties = new WorkbookProperties { Date1904 = uses1904DateSystem };

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(
                new NumberingFormats(),
                new Fonts(new Font()),
                new Fills(new Fill()),
                new Borders(new Border()),
                new CellStyleFormats(new CellFormat()),
                new CellFormats(
                    new CellFormat(),
                    new CellFormat
                    {
                        NumberFormatId = 14,
                        ApplyNumberFormat = true,
                    }));

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var row = new Row { RowIndex = 1 };
            row.Append(new Cell
            {
                CellReference = "A1",
                StyleIndex = 1,
                CellValue = new CellValue(dateSerial.ToString(CultureInfo.InvariantCulture)),
            });
            sheetData.Append(row);
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildDateFormulaCellXlsx(double cachedSerial)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(
                new NumberingFormats(),
                new Fonts(new Font()),
                new Fills(new Fill()),
                new Borders(new Border()),
                new CellStyleFormats(new CellFormat()),
                new CellFormats(
                    new CellFormat(),
                    new CellFormat
                    {
                        NumberFormatId = 14,
                        ApplyNumberFormat = true,
                    }));

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var row = new Row { RowIndex = 1 };
            row.Append(new Cell
            {
                CellReference = "A1",
                StyleIndex = 1,
                CellFormula = new CellFormula("TODAY()"),
                CellValue = new CellValue(cachedSerial.ToString(CultureInfo.InvariantCulture)),
            });
            sheetData.Append(row);
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildInlineStringXlsx(string cellReference, string value)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var row = new Row { RowIndex = 1 };
            row.Append(new Cell
            {
                CellReference = cellReference,
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value)),
            });
            sheetData.Append(row);

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildWorkbookWithSheets(
        IEnumerable<(string SheetName, IEnumerable<IReadOnlyList<string>> Rows)> sheets)
    {
        var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var sheetsElement = workbookPart.Workbook.AppendChild(new Sheets());
            uint sheetId = 1;

            foreach (var (sheetName, rows) in sheets)
            {
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();

                uint rowIndex = 1;
                foreach (var rowValues in rows)
                {
                    var row = new Row { RowIndex = rowIndex++ };
                    for (int i = 0; i < rowValues.Count; i++)
                    {
                        row.Append(new Cell
                        {
                            CellReference = $"{(char)('A' + i)}{row.RowIndex!.Value}",
                            DataType = CellValues.String,
                            CellValue = new CellValue(rowValues[i]),
                        });
                    }

                    sheetData.Append(row);
                }

                worksheetPart.Worksheet = new Worksheet(sheetData);

                sheetsElement.Append(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId++,
                    Name = sheetName,
                });
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// A non-seekable stream that yields a repeating byte value for a specified total length.
    /// Used to simulate large payloads without allocating the full byte array.
    /// </summary>
    private sealed class NonSeekableRepeatingStream(byte byteValue, long totalLength) : Stream
    {
        private long _remaining = totalLength;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;

            int toRead = (int)Math.Min(count, _remaining);
            Array.Fill(buffer, byteValue, offset, toRead);
            _remaining -= toRead;
            return toRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
