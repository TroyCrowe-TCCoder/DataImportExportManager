namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;

public class TsvExporterTests
{
    private readonly TsvExporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsTsv()
    {
        Assert.Equal(".tsv", _sut.SupportedExtension);
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
    public async Task WhenRowsProvidedThenWritesTabDelimitedContent()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["H1", "H2"], ["A", "B"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        using var reader = new StreamReader(destination, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync();

        Assert.Contains("H1\tH2", text);
        Assert.Contains("A\tB", text);
    }

    [Fact]
    public async Task WhenExportedThenTsvImporterRoundTripMatchesRows()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["Name", "Value"], ["O'Neil", "ACME-01"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        var importer = new TsvImporter();
        var roundTrip = await importer.ImportAsync(destination);

        Assert.Equal(data.Count, roundTrip.Count);
        Assert.Equal(data[0], roundTrip[0]);
        Assert.Equal(data[1], roundTrip[1]);
    }
}
