namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Importers;

/// <summary>
/// Unit tests for <see cref="CsvImporter"/>, validating RFC 4180 parsing including
/// multi-line quoted fields, configurable options, and cancellation support.
/// </summary>
public class CsvImporterTests
{
    private readonly CsvImporter _sut = new();

    [Fact]
    public void WhenSupportedExtensionThenReturnsCsv()
    {
        Assert.Equal(".csv", _sut.SupportedExtension);
    }

    [Fact]
    public async Task WhenStreamIsNullThenThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ImportAsync(null!).AsTask());
    }

    [Fact]
    public async Task WhenEmptyStreamThenReturnsEmptyList()
    {
        using var stream = new MemoryStream(""u8.ToArray());

        var result = await _sut.ImportAsync(stream);

        Assert.Empty(result);
    }

    [Fact]
    public async Task WhenSingleRowThenReturnsSingleRow()
    {
        using var stream = ToStream("Name,Age,City");

        var result = await _sut.ImportAsync(stream);

        Assert.Single(result);
        Assert.Equal(["Name", "Age", "City"], result[0]);
    }

    [Fact]
    public async Task WhenMultipleRowsThenReturnsAllRows()
    {
        using var stream = ToStream("A,B\n1,2\n3,4");

        var result = await _sut.ImportAsync(stream);

        Assert.Equal(3, result.Count);
        Assert.Equal(["A", "B"], result[0]);
        Assert.Equal(["1", "2"], result[1]);
        Assert.Equal(["3", "4"], result[2]);
    }

    [Fact]
    public async Task WhenQuotedFieldContainsCommaThenParsesCorrectly()
    {
        using var stream = ToStream("\"Hello, World\",B");

        var result = await _sut.ImportAsync(stream);

        Assert.Single(result);
        Assert.Equal("Hello, World", result[0][0]);
        Assert.Equal("B", result[0][1]);
    }

    [Fact]
    public async Task WhenQuotedFieldContainsEscapedQuoteThenParsesCorrectly()
    {
        using var stream = ToStream("\"She said \"\"hi\"\"\",B");

        var result = await _sut.ImportAsync(stream);

        Assert.Single(result);
        Assert.Equal("She said \"hi\"", result[0][0]);
    }

    [Fact]
    public async Task WhenQuotedFieldContainsCrLfThenParsesAsOneRow()
    {
        // RFC 4180 §2.6: a quoted field may span multiple lines.
        using var stream = ToStream("\"aaa\",\"b\r\nbb\",\"ccc\"");

        var result = await _sut.ImportAsync(stream);

        Assert.Single(result);
        Assert.Equal(3, result[0].Count);
        Assert.Equal("aaa", result[0][0]);
        Assert.Equal("b\r\nbb", result[0][1]);
        Assert.Equal("ccc", result[0][2]);
    }

    [Fact]
    public async Task WhenQuotedFieldContainsLfOnlyThenParsesAsOneRow()
    {
        using var stream = ToStream("\"line1\nline2\",next");

        var result = await _sut.ImportAsync(stream);

        Assert.Single(result);
        Assert.Equal("line1\nline2", result[0][0]);
        Assert.Equal("next", result[0][1]);
    }

    [Fact]
    public async Task WhenCrLfLineEndingsThenParsesAllRows()
    {
        using var stream = ToStream("A,B\r\n1,2\r\n3,4");

        var result = await _sut.ImportAsync(stream);

        Assert.Equal(3, result.Count);
        Assert.Equal(["A", "B"], result[0]);
        Assert.Equal(["1", "2"], result[1]);
        Assert.Equal(["3", "4"], result[2]);
    }

    [Fact]
    public async Task WhenCustomDelimiterThenParsesCorrectly()
    {
        var sut = new CsvImporter(new CsvImporterOptions { Delimiter = ';' });
        using var stream = ToStream("A;B;C\n1;2;3");

        var result = await sut.ImportAsync(stream);

        Assert.Equal(2, result.Count);
        Assert.Equal(["A", "B", "C"], result[0]);
        Assert.Equal(["1", "2", "3"], result[1]);
    }

    [Fact]
    public async Task WhenCancellationRequestedThenThrowsOperationCanceledException()
    {
        using var stream = ToStream("A,B\n1,2");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ImportAsync(stream, cts.Token).AsTask());
    }

    [Theory]
    [InlineData('"')]
    [InlineData('\r')]
    [InlineData('\n')]
    public void WhenDelimiterIsReservedCharacterThenThrowsArgumentException(char delimiter)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new CsvImporter(new CsvImporterOptions { Delimiter = delimiter }));

        Assert.Contains(":INVALID_DELIMITER]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenFieldsHaveLeadingOrTrailingSpacesThenTrimsWhitespace()
    {
        using var stream = ToStream(" Name , Age \n John , 30 ");

        var result = await _sut.ImportAsync(stream);

        Assert.Equal(2, result.Count);
        Assert.Equal(["Name", "Age"], result[0]);
        Assert.Equal(["John", "30"], result[1]);
    }

    private static MemoryStream ToStream(string content) =>
        new(Encoding.UTF8.GetBytes(content));
}

