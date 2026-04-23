namespace DataImportExportManager.Tests;

using System.Xml.Linq;
using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;

public class XmlExporterTests
{
    private readonly XmlExporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsXml()
    {
        Assert.Equal(".xml", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenDataIsNullThenThrowsArgumentNullException()
    {
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExportAsync(null!, destination).AsTask());
    }

    [Fact]
    public async Task WhenDestinationIsNullThenThrowsArgumentNullException()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["A"]];

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExportAsync(data, null!).AsTask());
    }

    [Fact]
    public async Task WhenRowsProvidedThenWritesRowsXmlSchema()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["H1", "H2"], ["A", "B"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        var document = await XDocument.LoadAsync(destination, LoadOptions.None, CancellationToken.None);
        Assert.Equal("rows", document.Root?.Name.LocalName);
        var rows = document.Root!.Elements("row").ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("H1", rows[0].Elements("cell").First().Value);
        Assert.Equal("B", rows[1].Elements("cell").Skip(1).First().Value);
    }

    [Fact]
    public async Task WhenExportedThenXmlImporterRoundTripMatchesRows()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["Name", "Value"], ["O'Neil", "ACME-01"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        var importer = new XmlImporter();
        var roundTrip = await importer.ImportAsync(destination);

        Assert.Equal(data.Count, roundTrip.Count);
        Assert.Equal(data[0], roundTrip[0]);
        Assert.Equal(data[1], roundTrip[1]);
    }

    [Fact]
    public async Task WhenObjectElementModeEnabledThenWritesHeaderNamedElements()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["name", "code"], ["Ada", "1"], ["Lin", "2"]];
        using var destination = new MemoryStream();
        var sut = new XmlExporter(new XmlExporterOptions { UseObjectElementRows = true });

        await sut.ExportAsync(data, destination);
        destination.Position = 0;

        var document = await XDocument.LoadAsync(destination, LoadOptions.None, CancellationToken.None);
        var rows = document.Root!.Elements("row").ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Ada", rows[0].Element("name")?.Value);
        Assert.Equal("2", rows[1].Element("code")?.Value);
    }

    [Fact]
    public async Task WhenObjectElementModeAndHeadersInvalidThenThrowsInvalidOperationException()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["invalid name"], ["A"]];
        using var destination = new MemoryStream();
        var sut = new XmlExporter(new XmlExporterOptions { UseObjectElementRows = true });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExportAsync(data, destination).AsTask());

        Assert.Contains(":INVALID_HEADER]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenStrictObjectRowWidthEnabledAndRowWidthMismatchedThenThrowsWithRowDiagnostics()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["name", "code"], ["Ada"]];
        using var destination = new MemoryStream();
        var sut = new XmlExporter(new XmlExporterOptions
        {
            UseObjectElementRows = true,
            StrictObjectElementRowWidth = true,
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExportAsync(data, destination).AsTask());

        Assert.Contains("row 2", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":ROW_WIDTH_MISMATCH]", ex.Message, StringComparison.Ordinal);
    }
}
