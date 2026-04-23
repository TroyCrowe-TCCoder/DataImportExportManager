namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Importers;

public class NdjsonImporterTests
{
    private readonly NdjsonImporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsNdjson()
    {
        Assert.Equal(".ndjson", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenSourceIsNullThenThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ImportAsync(null!).AsTask());
    }

    [Fact]
    public async Task WhenNdjsonContainsArrayRecordsThenReturnsRows()
    {
        using var source = ToStream("[\"H1\",\"H2\"]\n[\"A\",\"B\"]\n");

        var result = await _sut.ImportAsync(source);

        Assert.Equal(2, result.Count);
        Assert.Equal(["H1", "H2"], result[0]);
        Assert.Equal(["A", "B"], result[1]);
    }

    [Fact]
    public async Task WhenNdjsonContainsObjectRecordsThenReturnsHeaderAndRowsDeterministically()
    {
        using var source = ToStream("{\"id\":\"1\",\"name\":\"Ada\"}\n{\"name\":\"Lin\",\"active\":true}\n");

        var result = await _sut.ImportAsync(source);

        Assert.Equal(3, result.Count);
        Assert.Equal(["id", "name", "active"], result[0]);
        Assert.Equal(["1", "Ada", ""], result[1]);
        Assert.Equal(["", "Lin", "true"], result[2]);
    }

    [Fact]
    public async Task WhenNdjsonMixesArrayAndObjectRecordsThenThrowsInvalidOperationException()
    {
        using var source = ToStream("[\"A\"]\n{\"a\":\"b\"}\n");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());
    }

    [Fact]
    public async Task WhenBlankLineEncounteredAndIgnoreBlankLinesDisabledThenThrowsInvalidOperationException()
    {
        using var source = ToStream("[\"A\"]\n\n");
        var sut = new NdjsonImporter(new NdjsonImporterOptions { IgnoreBlankLines = false });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ImportAsync(source).AsTask());
    }

    [Fact]
    public async Task WhenNdjsonLineIsPrimitiveThenThrowsInvalidOperationException()
    {
        using var source = ToStream("123\n");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());

        Assert.Contains(":INVALID_RECORD_KIND]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenNdjsonLineMalformedThenThrowsInvalidOperationExceptionWithDiagnosticCode()
    {
        using var source = ToStream("{\"id\":1\n");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());

        Assert.Contains(":MALFORMED_JSON]", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 1", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("byte position", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenLaterNdjsonLineMalformedThenThrowsWithCorrectLineNumber()
    {
        using var source = ToStream("{\"id\":1}\n{\"id\":2\n");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ImportAsync(source).AsTask());

        Assert.Contains(":MALFORMED_JSON]", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 2", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("byte position", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MemoryStream ToStream(string value)
        => new(Encoding.UTF8.GetBytes(value));
}
