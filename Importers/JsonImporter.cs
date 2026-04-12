namespace DataImportExportManager.Importers;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DataImportExportManager.Diagnostics;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Imports tabular data from JSON streams.
/// Supports both array-of-arrays and array-of-objects payloads.
/// </summary>
public sealed partial class JsonImporter : IDataImporter
{
    private readonly JsonImporterOptions _options;
    private readonly ILogger<JsonImporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonImporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling object-record header generation.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public JsonImporter(JsonImporterOptions? options = null, ILogger<JsonImporter>? logger = null)
    {
        _options = options ?? new JsonImporterOptions();
        _logger = logger ?? NullLogger<JsonImporter>.Instance;
    }

    /// <inheritdoc />
    public string SupportedExtension => ".json";

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        LogImportStarted();
        var startTimestamp = Stopwatch.GetTimestamp();

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MalformedJson, "JSON payload is malformed."),
                ex);
        }

        using (document)
        {
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.InvalidRootKind, "JSON import requires a root array of arrays or objects."));
        }

        var results = root.GetArrayLength() == 0
            ? []
            : root[0].ValueKind switch
            {
                JsonValueKind.Array => ReadArrayRows(root, cancellationToken),
                JsonValueKind.Object => ReadObjectRows(root, _options.IncludeHeaderRowForObjectRecords, cancellationToken),
                _ => throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.InvalidRecordKind, "JSON root array elements must be arrays or objects.")),
            };

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogImportCompleted(results.Count, elapsedMs);
        return results;
        }
    }

    private static List<IReadOnlyList<string>> ReadArrayRows(JsonElement root, CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyList<string>>(root.GetArrayLength());

        foreach (var element in root.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MixedRecordTypes, "JSON root array cannot mix arrays with other element types."));
            }

            var row = new List<string>(element.GetArrayLength());
            foreach (var cell in element.EnumerateArray())
            {
                row.Add(ToCellValue(cell));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static List<IReadOnlyList<string>> ReadObjectRows(
        JsonElement root,
        bool includeHeaderRow,
        CancellationToken cancellationToken)
    {
        var headers = new List<string>();
        var headerSet = new HashSet<string>(StringComparer.Ordinal);
        var objectRows = new List<Dictionary<string, string>>(root.GetArrayLength());

        foreach (var element in root.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MixedRecordTypes, "JSON root array cannot mix objects with other element types."));
            }

            var rowValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (headerSet.Add(property.Name))
                {
                    headers.Add(property.Name);
                }

                rowValues[property.Name] = ToCellValue(property.Value);
            }

            objectRows.Add(rowValues);
        }

        var results = new List<IReadOnlyList<string>>(objectRows.Count + (includeHeaderRow ? 1 : 0));
        if (includeHeaderRow)
        {
            results.Add(headers);
        }

        foreach (var objectRow in objectRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new List<string>(headers.Count);
            foreach (var header in headers)
            {
                row.Add(objectRow.GetValueOrDefault(header, string.Empty));
            }

            results.Add(row);
        }

        return results;
    }

    private static string ToCellValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => element.GetRawText(),
        };

    [LoggerMessage(Level = LogLevel.Debug, Message = "JSON import started")]
    private partial void LogImportStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "JSON import completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogImportCompleted(int rowCount, double elapsedMs);

    private const string SupportedExtensionValue = ".json";
}
