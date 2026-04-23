namespace DataImportExportManager.Tests;

using System.Text.Json;
using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;

public class JsonExporterTests
{
    private readonly JsonExporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsJson()
    {
        Assert.Equal(".json", _sut.SupportedExtension);
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
    public async Task WhenRowsProvidedThenWritesJsonArrayOfArrays()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["H1", "H2"], ["A", "B"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        using var document = await JsonDocument.ParseAsync(destination);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal("H1", document.RootElement[0][0].GetString());
        Assert.Equal("B", document.RootElement[1][1].GetString());
    }

    [Fact]
    public async Task WhenIndentedOptionEnabledThenWritesIndentedJson()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["A", "B"]];
        using var destination = new MemoryStream();
        var sut = new JsonExporter(new JsonExporterOptions { WriteIndented = true });

        await sut.ExportAsync(data, destination);
        destination.Position = 0;
        using var reader = new StreamReader(destination, leaveOpen: true);
        var json = await reader.ReadToEndAsync();

        Assert.Contains(Environment.NewLine, json);
    }

    [Fact]
    public async Task WhenExportedThenJsonImporterRoundTripMatchesRows()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["Name", "Value"], ["O'Neil", "ACME-01"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        var importer = new JsonImporter();
        var roundTrip = await importer.ImportAsync(destination);

        Assert.Equal(data.Count, roundTrip.Count);
        Assert.Equal(data[0], roundTrip[0]);
        Assert.Equal(data[1], roundTrip[1]);
    }
}
