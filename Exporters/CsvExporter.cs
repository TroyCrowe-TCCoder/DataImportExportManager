namespace DataImportExportManager.Exporters;

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exports tabular data to a CSV-formatted stream. Fields containing the configured
/// delimiter, quotes, or newlines are automatically escaped per RFC 4180 conventions.
/// </summary>
public sealed partial class CsvExporter : IDataExporter
{
    private readonly ILogger<CsvExporter> _logger;
    private readonly Encoding _encoding;
    private readonly char _delimiter;
    private readonly bool _sanitizeFormulaCells;
    private readonly SearchValues<char> _csvSpecialChars;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvExporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options for encoding, delimiter, and formula sanitization.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public CsvExporter(CsvExporterOptions? options = null, ILogger<CsvExporter>? logger = null)
    {
        _logger = logger ?? NullLogger<CsvExporter>.Instance;
        var resolved = options ?? new CsvExporterOptions();
        _encoding = resolved.Encoding;
        _delimiter = resolved.Delimiter;
        _sanitizeFormulaCells = resolved.SanitizeFormulaCells;
        _csvSpecialChars = SearchValues.Create([_delimiter, '"', '\n', '\r']);
        if (resolved.Delimiter is '"' or '\r' or '\n')
            throw new ArgumentException(
                "The delimiter cannot be a double-quote, carriage-return, or line-feed character.",
                nameof(options));
    }

    /// <inheritdoc />
    public string SupportedExtension => ".csv";

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask ExportAsync(
        IReadOnlyList<IReadOnlyList<string>> data, Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(destination);

        LogExportStarted(data.Count);
        var startTimestamp = Stopwatch.GetTimestamp();

        using var writer = new StreamWriter(destination, encoding: _encoding, leaveOpen: true);
        var lineBuffer = new StringBuilder();

        foreach (var row in data)
        {
            lineBuffer.Clear();

            for (int i = 0; i < row.Count; i++)
            {
                if (i > 0) lineBuffer.Append(_delimiter);
                WriteEscapedField(row[i] ?? string.Empty, lineBuffer);
            }

            lineBuffer.Append(writer.NewLine);
            await writer.WriteAsync(lineBuffer, cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogExportCompleted(data.Count, elapsedMs);
    }

    /// <summary>
    /// Writes <paramref name="field"/> into <paramref name="sb"/>, applying optional
    /// formula-injection sanitization and RFC 4180 quoting/escaping without
    /// intermediate string allocations.
    /// </summary>
    private void WriteEscapedField(string field, StringBuilder sb)
    {
        var sanitize = _sanitizeFormulaCells
            && field.Length > 0
            && field[0] is '=' or '+' or '-' or '@' or '\t' or '\r';

        var needsQuoting = field.AsSpan().IndexOfAny(_csvSpecialChars) >= 0;

        if (needsQuoting)
        {
            sb.Append('"');
            if (sanitize) sb.Append('\'');

            foreach (char c in field)
            {
                if (c == '"') sb.Append('"');
                sb.Append(c);
            }

            sb.Append('"');
        }
        else
        {
            if (sanitize) sb.Append('\'');
            sb.Append(field);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "CSV export started with {RowCount} rows")]
    private partial void LogExportStarted(int rowCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CSV export completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogExportCompleted(int rowCount, double elapsedMs);
}

