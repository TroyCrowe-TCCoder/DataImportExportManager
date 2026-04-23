namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Importers;

public class XmlImporterTests
{
    private readonly XmlImporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsXml()
    {
        Assert.Equal(".xml", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenSourceIsNullThenThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ImportAsync(null!).AsTask());
    }

    [Fact]
    public async Task WhenRowsXmlProvidedThenReturnsRows()
    {
        using var source = ToStream("<?xml version=\"1.0\" encoding=\"utf-8\"?><rows><row><cell>H1</cell><cell>H2</cell></row><row><cell>A</cell><cell>B</cell></row></rows>");

        var result = await _sut.ImportAsync(source);

        Assert.Equal(2, result.Count);
        Assert.Equal(["H1", "H2"], result[0]);
        Assert.Equal(["A", "B"], result[1]);
    }

    [Fact]
    public async Task WhenRootIsNotRowsThenThrowsInvalidOperationException()
    {
        using var source = ToStream("<root><row><cell>A</cell></row></root>");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());
    }

    [Fact]
    public async Task WhenXmlMalformedThenThrowsInvalidOperationExceptionWithDiagnosticCode()
    {
        using var source = ToStream("<rows><row><cell>A</cell></row>");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());

        Assert.Contains(":MALFORMED_XML]", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("position", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenRowSchemaModeCellsOnlyAndObjectRowThenThrowsWithRowDiagnostics()
    {
        using var source = ToStream("<rows><row><name>Ada</name></row></rows>");
        var sut = new XmlImporter(new XmlImporterOptions { RowSchemaMode = XmlImportRowSchemaMode.CellsOnly });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ImportAsync(source).AsTask());

        Assert.Contains("row 1", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":SCHEMA_MODE_VIOLATION]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenRowSchemaModeObjectElementsOnlyAndCellRowThenThrowsWithRowDiagnostics()
    {
        using var source = ToStream("<rows><row><cell>A</cell></row></rows>");
        var sut = new XmlImporter(new XmlImporterOptions { RowSchemaMode = XmlImportRowSchemaMode.ObjectElementsOnly });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ImportAsync(source).AsTask());

        Assert.Contains("row 1", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenObjectElementRowsThenReturnsDeterministicHeaderAndValues()
    {
        using var source = ToStream("<rows><row><name>Ada</name><code>1</code></row><row><code>2</code><active>true</active></row></rows>");

        var result = await _sut.ImportAsync(source);

        Assert.Equal(3, result.Count);
        Assert.Equal(["name", "code", "active"], result[0]);
        Assert.Equal(["Ada", "1", ""], result[1]);
        Assert.Equal(["", "2", "true"], result[2]);
    }

    [Fact]
    public async Task WhenObjectElementRowsDisabledThenThrowsInvalidOperationException()
    {
        using var source = ToStream("<rows><row><name>Ada</name></row></rows>");
        var sut = new XmlImporter(new XmlImporterOptions { EnableObjectElementRows = false });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ImportAsync(source).AsTask());
    }

    [Fact]
    public async Task WhenMixedCellAndObjectRowsThenThrowsInvalidOperationException()
    {
        using var source = ToStream("<rows><row><cell>A</cell></row><row><name>B</name></row></rows>");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());
    }

    private static MemoryStream ToStream(string xml)
        => new(Encoding.UTF8.GetBytes(xml));
}
