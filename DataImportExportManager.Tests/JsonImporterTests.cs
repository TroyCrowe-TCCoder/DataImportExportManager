namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Importers;

public class JsonImporterTests
{
    private readonly JsonImporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsJson()
    {
        Assert.Equal(".json", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenSourceIsNullThenThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ImportAsync(null!).AsTask());
    }

    [Fact]
    public async Task WhenJsonIsArrayOfArraysThenReturnsRows()
    {
        using var source = ToStream("[[\"H1\",\"H2\"],[\"A\",\"B\"]]");

        var result = await _sut.ImportAsync(source);

        Assert.Equal(2, result.Count);
        Assert.Equal(["H1", "H2"], result[0]);
        Assert.Equal(["A", "B"], result[1]);
    }

    [Fact]
    public async Task WhenJsonIsArrayOfObjectsThenReturnsHeaderAndRowsDeterministically()
    {
        using var source = ToStream("[{\"id\":\"1\",\"name\":\"Ada\"},{\"name\":\"Lin\",\"extra\":true}]");

        var result = await _sut.ImportAsync(source);

        Assert.Equal(3, result.Count);
        Assert.Equal(["id", "name", "extra"], result[0]);
        Assert.Equal(["1", "Ada", ""], result[1]);
        Assert.Equal(["", "Lin", "true"], result[2]);
    }

    [Fact]
    public async Task WhenHeaderOptionDisabledForObjectRecordsThenReturnsDataRowsOnly()
    {
        using var source = ToStream("[{\"id\":\"1\",\"name\":\"Ada\"},{\"name\":\"Lin\"}]");
        var sut = new JsonImporter(new JsonImporterOptions { IncludeHeaderRowForObjectRecords = false });

        var result = await sut.ImportAsync(source);

        Assert.Equal(2, result.Count);
        Assert.Equal(["1", "Ada"], result[0]);
        Assert.Equal(["", "Lin"], result[1]);
    }

    [Fact]
    public async Task WhenJsonRootIsNotArrayThenThrowsInvalidOperationException()
    {
        using var source = ToStream("{\"id\":1}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());

        Assert.Contains(":INVALID_ROOT_KIND]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenJsonMalformedThenThrowsInvalidOperationExceptionWithDiagnosticCode()
    {
        using var source = ToStream("[{]");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());

        Assert.Contains(":MALFORMED_JSON]", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("byte position", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MemoryStream ToStream(string json)
        => new(Encoding.UTF8.GetBytes(json));
}
