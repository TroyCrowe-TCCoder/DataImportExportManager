namespace DataImportExportManager.Exporters;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Xml;
using DataImportExportManager.Diagnostics;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Exports tabular data to XML using a deterministic schema:
/// <c>&lt;rows&gt;&lt;row&gt;&lt;cell&gt;value&lt;/cell&gt;...&lt;/row&gt;...&lt;/rows&gt;</c>.
/// </summary>
public sealed partial class XmlExporter : IDataExporter
{
    private readonly XmlExporterOptions _options;
    private readonly ILogger<XmlExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlExporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling XML row-shape output.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public XmlExporter(XmlExporterOptions? options = null, ILogger<XmlExporter>? logger = null)
    {
        _options = options ?? new XmlExporterOptions();
        _logger = logger ?? NullLogger<XmlExporter>.Instance;
    }

    /// <inheritdoc />
    public string SupportedExtension => ".xml";

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

        var settings = new XmlWriterSettings
        {
            Async = true,
            CloseOutput = false,
            Indent = false,
            OmitXmlDeclaration = false,
        };

        using var writer = XmlWriter.Create(destination, settings);
        await writer.WriteStartDocumentAsync().ConfigureAwait(false);
        await writer.WriteStartElementAsync(null, "rows", null).ConfigureAwait(false);

        if (_options.UseObjectElementRows)
        {
            var headers = data.Count > 0 ? data[0] : [];
            ValidateHeaders(headers);

            for (var rowIndex = 1; rowIndex < data.Count; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = data[rowIndex];
                if (_options.StrictObjectElementRowWidth && row.Count != headers.Count)
                {
                    throw new InvalidOperationException(
                        ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Export, ContractDiagnostics.Codes.RowWidthMismatch, $"XML object-element export row {rowIndex + 1} has {row.Count} values but header defines {headers.Count} columns."));
                }

                await writer.WriteStartElementAsync(null, "row", null).ConfigureAwait(false);
                for (var i = 0; i < headers.Count; i++)
                {
                    var value = i < row.Count ? row[i] ?? string.Empty : string.Empty;
                    await writer.WriteStartElementAsync(null, headers[i], null).ConfigureAwait(false);
                    await writer.WriteStringAsync(value).ConfigureAwait(false);
                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }
        }
        else
        {
            foreach (var row in data)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await writer.WriteStartElementAsync(null, "row", null).ConfigureAwait(false);
                foreach (var cell in row)
                {
                    await writer.WriteStartElementAsync(null, "cell", null).ConfigureAwait(false);
                    await writer.WriteStringAsync(cell ?? string.Empty).ConfigureAwait(false);
                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
        await writer.WriteEndDocumentAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogExportCompleted(data.Count, elapsedMs);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "XML export started with {RowCount} rows")]
    private partial void LogExportStarted(int rowCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "XML export completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogExportCompleted(int rowCount, double elapsedMs);

    private static void ValidateHeaders(IReadOnlyList<string> headers)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Export, ContractDiagnostics.Codes.InvalidHeader, "XML object-element export requires non-empty header names."));
            }

            var trimmed = header.Trim();
            try
            {
                XmlConvert.VerifyName(trimmed);
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Export, ContractDiagnostics.Codes.InvalidHeader, "XML object-element export header names must be valid XML element names."),
                    ex);
            }

            if (!names.Add(trimmed))
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Export, ContractDiagnostics.Codes.DuplicateHeader, "XML object-element export requires unique header names."));
            }
        }
    }

    private const string SupportedExtensionValue = ".xml";
}
