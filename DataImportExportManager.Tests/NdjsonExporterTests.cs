namespace DataImportExportManager.Tests;

using System.Text;
using System.Text.Json;
using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;

public class NdjsonExporterTests
{
    private readonly NdjsonExporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsNdjson()
    {
        Assert.Equal(".ndjson", _sut.SupportedExtension);
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
    public async Task WhenRowsProvidedThenWritesOneJsonArrayPerLine()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["H1", "H2"], ["A", "B"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        using var reader = new StreamReader(destination, Encoding.UTF8, leaveOpen: true);
        var firstLine = await reader.ReadLineAsync();
        var secondLine = await reader.ReadLineAsync();

        Assert.NotNull(firstLine);
        Assert.NotNull(secondLine);

        using var firstDoc = JsonDocument.Parse(firstLine!);
        using var secondDoc = JsonDocument.Parse(secondLine!);

        Assert.Equal("H1", firstDoc.RootElement[0].GetString());
        Assert.Equal("B", secondDoc.RootElement[1].GetString());
    }

    [Fact]
    public async Task WhenTrailingNewlineDisabledThenDoesNotEndWithLineBreak()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["A"], ["B"]];
        using var destination = new MemoryStream();
        var sut = new NdjsonExporter(new NdjsonExporterOptions { WriteTrailingNewline = false });

        await sut.ExportAsync(data, destination);
        destination.Position = 0;

        using var reader = new StreamReader(destination, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync();

        Assert.False(text.EndsWith(Environment.NewLine, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenExportedThenNdjsonImporterRoundTripMatchesRows()
    {
        IReadOnlyList<IReadOnlyList<string>> data = [["Name", "Value"], ["O'Neil", "ACME-01"]];
        using var destination = new MemoryStream();

        await _sut.ExportAsync(data, destination);
        destination.Position = 0;

        var importer = new NdjsonImporter();
        var roundTrip = await importer.ImportAsync(destination);

        Assert.Equal(data.Count, roundTrip.Count);
        Assert.Equal(data[0], roundTrip[0]);
        Assert.Equal(data[1], roundTrip[1]);
    }
}
