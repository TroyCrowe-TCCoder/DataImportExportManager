namespace DataImportExportManager.Exporters;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exports tabular data to NDJSON/JSONL streams using one JSON array per line.
/// </summary>
public sealed partial class NdjsonExporter : IDataExporter
{
    private readonly NdjsonExporterOptions _options;
    private readonly ILogger<NdjsonExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NdjsonExporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling NDJSON trailing-newline behavior.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public NdjsonExporter(NdjsonExporterOptions? options = null, ILogger<NdjsonExporter>? logger = null)
    {
        _options = options ?? new NdjsonExporterOptions();
        _logger = logger ?? NullLogger<NdjsonExporter>.Instance;
    }

    /// <inheritdoc />
    public string SupportedExtension => ".ndjson";

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask ExportAsync(
        IReadOnlyList<IReadOnlyList<string>> data,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(destination);

        LogExportStarted(data.Count);
        var startTimestamp = Stopwatch.GetTimestamp();

        using var writer = new StreamWriter(destination, Encoding.UTF8, leaveOpen: true);

        for (var i = 0; i < data.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = data[i];
            var serialized = JsonSerializer.Serialize(row.Select(value => value ?? string.Empty));

            if (i < data.Count - 1 || _options.WriteTrailingNewline)
            {
                await writer.WriteLineAsync(serialized.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await writer.WriteAsync(serialized.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogExportCompleted(data.Count, elapsedMs);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "NDJSON export started with {RowCount} rows")]
    private partial void LogExportStarted(int rowCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "NDJSON export completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogExportCompleted(int rowCount, double elapsedMs);
}
