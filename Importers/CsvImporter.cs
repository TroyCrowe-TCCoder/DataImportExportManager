namespace DataImportExportManager.Importers;

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Imports tabular data from a CSV-formatted stream. Handles quoted fields,
/// escaped quotes, newlines inside quoted fields, and configurable delimiters
/// and encodings per RFC 4180 conventions.
/// </summary>
public sealed partial class CsvImporter : IDataImporter
{
    private readonly ILogger<CsvImporter> _logger;
    private readonly Encoding _encoding;
    private readonly char _delimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvImporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options for encoding and delimiter.
    /// When <see langword="null"/>, defaults are used (UTF-8 encoding, comma delimiter).
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public CsvImporter(CsvImporterOptions? options = null, ILogger<CsvImporter>? logger = null)
    {
        _logger = logger ?? NullLogger<CsvImporter>.Instance;
        var resolved = options ?? new CsvImporterOptions();
        _encoding = resolved.Encoding;
        _delimiter = resolved.Delimiter;
        if (resolved.Delimiter is '"' or '\r' or '\n')
            throw new ArgumentException(
                "The delimiter cannot be a double-quote, carriage-return, or line-feed character.",
                nameof(options));
    }

    /// <inheritdoc />
    public string SupportedExtension => ".csv";

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        LogImportStarted();
        var startTimestamp = Stopwatch.GetTimestamp();

        var results = new List<IReadOnlyList<string>>();
        var fieldBuffer = new StringBuilder();
        var currentRow = new List<string>();
        var inQuotes = false;
        var pendingEndQuote = false;
        var skipNextLf = false;
        var columnHint = 0;

        // Rent a char buffer for chunked reading; avoids per-read allocations.
        var charBuffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            using var reader = new StreamReader(source, encoding: _encoding, leaveOpen: true);
            int charsRead;

            while ((charsRead = await reader.ReadAsync(charBuffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                for (var i = 0; i < charsRead; i++)
                {
                    var c = charBuffer[i];

                    if (inQuotes)
                    {
                        if (pendingEndQuote)
                        {
                            // The previous char was a closing quote — determine if it was
                            // an escaped "" or the actual end of the quoted field.
                            pendingEndQuote = false;
                            if (c == '"')
                            {
                                fieldBuffer.Append('"');
                                continue;
                            }

                            // Quoted field has closed; fall through to unquoted processing.
                            inQuotes = false;
                        }
                        else if (c == '"')
                        {
                            // Could be an escaped "" or a closing quote; decide on next char.
                            pendingEndQuote = true;
                            continue;
                        }
                        else
                        {
                            // Newlines inside a quoted field are literal per RFC 4180 §2.6.
                            fieldBuffer.Append(c);
                            continue;
                        }
                    }

                    // Unquoted character processing.
                    if (c == '"')
                    {
                        inQuotes = true;
                        skipNextLf = false;
                    }
                    else if (c == _delimiter)
                    {
                        currentRow.Add(fieldBuffer.ToString());
                        fieldBuffer.Clear();
                        skipNextLf = false;
                    }
                    else if (c == '\r')
                    {
                        currentRow.Add(fieldBuffer.ToString());
                        fieldBuffer.Clear();
                        if (columnHint == 0 && currentRow.Count > 0) columnHint = currentRow.Count;
                        results.Add(currentRow);
                        currentRow = new List<string>(columnHint > 0 ? columnHint : 4);
                        skipNextLf = true;
                    }
                    else if (c == '\n')
                    {
                        if (!skipNextLf)
                        {
                            currentRow.Add(fieldBuffer.ToString());
                            fieldBuffer.Clear();
                            if (columnHint == 0 && currentRow.Count > 0) columnHint = currentRow.Count;
                            results.Add(currentRow);
                            currentRow = new List<string>(columnHint > 0 ? columnHint : 4);
                        }

                        skipNextLf = false;
                    }
                    else
                    {
                        skipNextLf = false;
                        fieldBuffer.Append(c);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(charBuffer);
        }

        // A trailing closing quote at end-of-stream (no record terminator) closes the field.
        if (pendingEndQuote)
        {
            inQuotes = false;
        }

        // Flush the final row when the stream does not end with a record terminator.
        if (currentRow.Count > 0 || fieldBuffer.Length > 0)
        {
            currentRow.Add(fieldBuffer.ToString());
            results.Add(currentRow);
        }

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogImportCompleted(results.Count, elapsedMs);
        return results;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "CSV import started")]
    private partial void LogImportStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "CSV import completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogImportCompleted(int rowCount, double elapsedMs);
}

