namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Importers;

public class TsvImporterTests
{
    private readonly TsvImporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsTsv()
    {
        Assert.Equal(".tsv", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenSourceIsNullThenThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ImportAsync(null!).AsTask());
    }

    [Fact]
    public async Task WhenTsvDataThenParsesUsingTabDelimiter()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("H1\tH2\nA\tB\n"));

        var result = await _sut.ImportAsync(source);

        Assert.Equal(2, result.Count);
        Assert.Equal(["H1", "H2"], result[0]);
        Assert.Equal(["A", "B"], result[1]);
    }
}
