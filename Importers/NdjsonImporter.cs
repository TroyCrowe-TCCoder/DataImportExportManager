namespace DataImportExportManager.Importers;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DataImportExportManager.Diagnostics;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Imports tabular data from NDJSON/JSONL streams.
/// Supports line-delimited JSON arrays or JSON objects, using deterministic ordering rules.
/// </summary>
public sealed partial class NdjsonImporter : IDataImporter
{
    private readonly NdjsonImporterOptions _options;
    private readonly ILogger<NdjsonImporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NdjsonImporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling blank-line behavior.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public NdjsonImporter(NdjsonImporterOptions? options = null, ILogger<NdjsonImporter>? logger = null)
    {
        _options = options ?? new NdjsonImporterOptions();
        _logger = logger ?? NullLogger<NdjsonImporter>.Instance;
    }

    /// <inheritdoc />
    public string SupportedExtension => ".ndjson";

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        LogImportStarted();
        var startTimestamp = Stopwatch.GetTimestamp();

        using var reader = new StreamReader(source, leaveOpen: true);

        var arrayRows = new List<IReadOnlyList<string>>();
        var objectRows = new List<Dictionary<string, string>>();
        var headers = new List<string>();
        var headerSet = new HashSet<string>(StringComparer.Ordinal);

        JsonValueKind? recordKind = null;
        var lineNumber = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                if (!_options.IgnoreBlankLines)
                {
                    throw new InvalidOperationException(
                        ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.BlankLine, $"NDJSON line {lineNumber} is blank."));
                }

                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MalformedJson, $"NDJSON line {lineNumber} is malformed JSON."),
                    ex);
            }

            using (document)
            {
            var root = document.RootElement;

            if (root.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.InvalidRecordKind, $"NDJSON line {lineNumber} must be a JSON array or object."));
            }

            recordKind ??= root.ValueKind;
            if (recordKind != root.ValueKind)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MixedRecordTypes, "NDJSON input cannot mix array and object record types."));
            }

            if (recordKind == JsonValueKind.Array)
            {
                var row = new List<string>(root.GetArrayLength());
                foreach (var element in root.EnumerateArray())
                {
                    row.Add(ToCellValue(element));
                }

                arrayRows.Add(row);
                continue;
            }

            var rowValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (headerSet.Add(property.Name))
                {
                    headers.Add(property.Name);
                }

                rowValues[property.Name] = ToCellValue(property.Value);
            }

            objectRows.Add(rowValues);
            }
        }

        List<IReadOnlyList<string>> results;
        if (recordKind == JsonValueKind.Object && headers.Count > 0)
        {
            var objectResults = new List<IReadOnlyList<string>>(objectRows.Count + 1)
            {
                headers,
            };

            foreach (var rowValues in objectRows)
            {
                var row = new List<string>(headers.Count);
                foreach (var header in headers)
                {
                    row.Add(rowValues.GetValueOrDefault(header, string.Empty));
                }

                objectResults.Add(row);
            }

            results = objectResults;
        }
        else
        {
            results = arrayRows;
        }

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogImportCompleted(results.Count, elapsedMs);
        return results;
    }

    private static string ToCellValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => element.GetRawText(),
        };

    [LoggerMessage(Level = LogLevel.Debug, Message = "NDJSON import started")]
    private partial void LogImportStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "NDJSON import completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogImportCompleted(int rowCount, double elapsedMs);

    private const string SupportedExtensionValue = ".ndjson";
}
