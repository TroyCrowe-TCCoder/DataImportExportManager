namespace DataImportExportManager.Exporters;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exports tabular data to JSON as an array of arrays.
/// </summary>
public sealed partial class JsonExporter : IDataExporter
{
    private readonly JsonExporterOptions _options;
    private readonly ILogger<JsonExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonExporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling JSON output formatting.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public JsonExporter(JsonExporterOptions? options = null, ILogger<JsonExporter>? logger = null)
    {
        _options = options ?? new JsonExporterOptions();
        _logger = logger ?? NullLogger<JsonExporter>.Instance;
    }

    /// <inheritdoc />
    public string SupportedExtension => ".json";

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

        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions
        {
            Indented = _options.WriteIndented,
        });

        writer.WriteStartArray();

        foreach (var row in data)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteStartArray();
            foreach (var cell in row)
            {
                writer.WriteStringValue(cell ?? string.Empty);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogExportCompleted(data.Count, elapsedMs);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "JSON export started with {RowCount} rows")]
    private partial void LogExportStarted(int rowCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "JSON export completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogExportCompleted(int rowCount, double elapsedMs);
}
