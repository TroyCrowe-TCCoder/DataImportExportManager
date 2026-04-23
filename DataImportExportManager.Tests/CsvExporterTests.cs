namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Exporters;

/// <summary>
/// Unit tests for <see cref="CsvExporter"/>, validating CSV output formatting,
/// RFC 4180 field escaping, opt-in formula sanitization, and multi-row export.
/// </summary>
public class CsvExporterTests
{
    private readonly CsvExporter _sut = new();
    private readonly CsvExporter _sanitizingSut = new(new CsvExporterOptions { SanitizeFormulaCells = true });

    [Fact]
    public void WhenSupportedExtensionThenReturnsCsv()
    {
        Assert.Equal(".csv", _sut.SupportedExtension);
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
        var data = new List<IReadOnlyList<string>>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExportAsync(data, null!).AsTask());
    }

    [Fact]
    public async Task WhenEmptyDataThenWritesNothing()
    {
        using var stream = new MemoryStream();
        var data = new List<IReadOnlyList<string>>();

        await _sut.ExportAsync(data, stream);

        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task WhenSingleRowThenWritesCsvLine()
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [["A", "B", "C"]];

        await _sut.ExportAsync(data, stream);

        string result = ReadStream(stream);
        Assert.Equal("A,B,C", result.TrimEnd());
    }

    [Fact]
    public async Task WhenFieldContainsCommaThenQuotesField()
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [["Hello, World", "B"]];

        await _sut.ExportAsync(data, stream);

        string result = ReadStream(stream);
        Assert.Equal("\"Hello, World\",B", result.TrimEnd());
    }

    [Fact]
    public async Task WhenFieldContainsQuoteThenEscapesQuote()
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [["She said \"hi\"", "B"]];

        await _sut.ExportAsync(data, stream);

        string result = ReadStream(stream);
        Assert.Equal("\"She said \"\"hi\"\"\",B", result.TrimEnd());
    }

    [Fact]
    public async Task WhenMultipleRowsThenWritesAllRows()
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [["H1", "H2"], ["1", "2"], ["3", "4"]];

        await _sut.ExportAsync(data, stream);

        string result = ReadStream(stream);
        var lines = result.TrimEnd().Split(Environment.NewLine);
        Assert.Equal(3, lines.Length);
        Assert.Equal("H1,H2", lines[0]);
        Assert.Equal("1,2", lines[1]);
        Assert.Equal("3,4", lines[2]);
    }

    [Fact]
    public async Task WhenFieldContainsCarriageReturnThenQuotesField()
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [["line1\rline2"]];

        await _sut.ExportAsync(data, stream);

        string result = ReadStream(stream).TrimEnd();
        Assert.StartsWith("\"", result);
        Assert.EndsWith("\"", result);
    }

    [Fact]
    public async Task WhenCustomDelimiterThenWritesDelimitedOutput()
    {
        var sut = new CsvExporter(new CsvExporterOptions { Delimiter = ';' });
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [["A", "B", "C"]];

        await sut.ExportAsync(data, stream);

        string result = ReadStream(stream).TrimEnd();
        Assert.Equal("A;B;C", result);
    }

    // --- Formula sanitization is opt-in (SanitizeFormulaCells = true) ---

    [Theory]
    [InlineData("=1+2", "'=1+2")]
    [InlineData("+1+2", "'+1+2")]
    [InlineData("-1+2", "'-1+2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    public async Task WhenSanitizeEnabledAndFieldStartsWithFormulaPrefixThenPrefixed(string input, string expected)
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [[input]];

        await _sanitizingSut.ExportAsync(data, stream);

        string result = ReadStream(stream).TrimEnd();
        Assert.Equal(expected.Trim(), result);
    }

    [Fact]
    public async Task WhenSanitizeEnabledAndFieldStartsWithCarriageReturnThenPrefixedAndQuoted()
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [["\rcmd"]];

        await _sanitizingSut.ExportAsync(data, stream);

        string result = ReadStream(stream).TrimEnd();
        Assert.Equal("\"'\rcmd\"", result);
    }

    [Theory]
    [InlineData("=FORMULA")]
    [InlineData("+value")]
    [InlineData("-value")]
    [InlineData("@value")]
    public async Task WhenSanitizeDisabledThenFormulaCellsNotModified(string input)
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [[input]];

        await _sut.ExportAsync(data, stream);

        string result = ReadStream(stream).TrimEnd();
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData('"')]
    [InlineData('\r')]
    [InlineData('\n')]
    public void WhenDelimiterIsReservedCharacterThenThrowsArgumentException(char delimiter)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new CsvExporter(new CsvExporterOptions { Delimiter = delimiter }));

        Assert.Contains(":INVALID_DELIMITER]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenDataContainsNullValueThenExportsEmptyString()
    {
        using var stream = new MemoryStream();
        List<IReadOnlyList<string>> data = [[null!]];

        await _sut.ExportAsync(data, stream);

        string result = ReadStream(stream).TrimEnd();
        Assert.Equal(string.Empty, result);
    }

    private static string ReadStream(MemoryStream stream)
    {
        stream.Position = 0;
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

