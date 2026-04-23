namespace DataImportExportManager.Tests;

using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;
using System.Collections;

/// <summary>
/// Unit tests for <see cref="ExcelExporter"/>, validating null guards,
/// extension metadata, round-trip fidelity, custom sheet naming, and cancellation.
/// </summary>
public class ExcelExporterTests
{
    private readonly ExcelExporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsXlsx()
    {
        Assert.Equal(".xlsx", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenDataIsNullThenThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExportAsync(null!, stream).AsTask());
    }

    [Fact]
    public async Task WhenDestinationIsNullThenThrowsArgumentNullException()
    {
        List<IReadOnlyList<string>> data = [["A", "B"]];

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExportAsync(data, null!).AsTask());
    }

    [Fact]
    public async Task WhenExportedThenImportRoundTripPreservesAllRows()
    {
        // Arrange in a unit test — one line, no ceremony:
        List<IReadOnlyList<string>> data = [["Header1", "Header2"], ["Value1", "Value2"]];
        using var stream = new MemoryStream();

        await _sut.ExportAsync(data, stream);
        stream.Position = 0;

        var importer = new ExcelImporter();
        var actual = await importer.ImportAsync(stream);

        Assert.Equal(data.Count, actual.Count);
        for (int i = 0; i < data.Count; i++)
        {
            Assert.Equal(data[i], actual[i]);
        }
    }

    [Fact]
    public async Task WhenCustomSheetNameThenImportByNameSucceeds()
    {
        const string sheetName = "MyData";
        var sut = new ExcelExporter(new ExcelExporterOptions { SheetName = sheetName });
        List<IReadOnlyList<string>> data = [["Col"]];
        using var stream = new MemoryStream();

        await sut.ExportAsync(data, stream);
        stream.Position = 0;

        var importer = new ExcelImporter(new ExcelImporterOptions { SheetName = sheetName });
        var rows = await importer.ImportAsync(stream);

        Assert.Single(rows);
    }

    [Fact]
    public async Task WhenCancellationRequestedThenThrowsOperationCanceledException()
    {
        List<IReadOnlyList<string>> data = Enumerable
            .Range(0, 500)
            .Select(i => (IReadOnlyList<string>)Enumerable.Range(0, 10).Select(j => $"R{i}C{j}").ToList())
            .ToList();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ExportAsync(data, new MemoryStream(), cts.Token).AsTask());
    }

    [Fact]
    public async Task WhenSheetNameIsEmptyThenThrowsArgumentException()
    {
        var sut = new ExcelExporter(new ExcelExporterOptions { SheetName = string.Empty });
        List<IReadOnlyList<string>> data = [["A"]];
        using var stream = new MemoryStream();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExportAsync(data, stream).AsTask());

        Assert.Contains(":INVALID_SHEET_NAME]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenDataExceedsExcelRowLimitThenThrowsArgumentOutOfRangeException()
    {
        // Uses a lightweight stub to avoid allocating 1 M+ rows in memory.
        var data = new CountOnlyList(1_048_577);
        using var stream = new MemoryStream();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.ExportAsync(data, stream).AsTask());

        Assert.Contains(":ROW_LIMIT_EXCEEDED]", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lightweight <see cref="IReadOnlyList{T}"/> stub that exposes only <see cref="Count"/>
    /// without storing any elements. Used to exercise the Excel row-limit guard without
    /// allocating millions of rows.
    /// </summary>
    private sealed class CountOnlyList(int count) : IReadOnlyList<IReadOnlyList<string>>
    {
        public int Count => count;
        public IReadOnlyList<string> this[int index] => throw new NotSupportedException();
        public IEnumerator<IReadOnlyList<string>> GetEnumerator() => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }
}
