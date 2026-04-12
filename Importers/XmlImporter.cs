namespace DataImportExportManager.Importers;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using DataImportExportManager.Diagnostics;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Imports tabular data from XML streams using a deterministic schema:
/// <c>&lt;rows&gt;&lt;row&gt;&lt;cell&gt;value&lt;/cell&gt;...&lt;/row&gt;...&lt;/rows&gt;</c>.
/// </summary>
public sealed partial class XmlImporter : IDataImporter
{
    private readonly XmlImporterOptions _options;
    private readonly ILogger<XmlImporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlImporter"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options controlling XML row-shape behavior.
    /// When <see langword="null"/>, defaults are used.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured diagnostics.
    /// When <see langword="null"/>, a <see cref="NullLogger{T}"/> is used.
    /// </param>
    public XmlImporter(XmlImporterOptions? options = null, ILogger<XmlImporter>? logger = null)
    {
        _options = options ?? new XmlImporterOptions();
        _logger = logger ?? NullLogger<XmlImporter>.Instance;
    }

    /// <inheritdoc />
    public string SupportedExtension => ".xml";

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        LogImportStarted();
        var startTimestamp = Stopwatch.GetTimestamp();

        XDocument document;
        try
        {
            document = await XDocument.LoadAsync(source, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        }
        catch (XmlException ex)
        {
            var detail = ex.LineNumber > 0 && ex.LinePosition > 0
                ? $"XML payload is malformed at line {ex.LineNumber}, position {ex.LinePosition}."
                : "XML payload is malformed.";

            throw new InvalidOperationException(
                ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MalformedXml, detail),
                ex);
        }

        var root = document.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "rows", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.InvalidRoot, "XML import requires a root element named 'rows'."));
        }

        var cellRows = new List<IReadOnlyList<string>>();
        var objectRows = new List<Dictionary<string, string>>();
        var headers = new List<string>();
        var headerSet = new HashSet<string>(StringComparer.Ordinal);
        XmlRowMode mode = XmlRowMode.Unknown;

        var rowIndex = 0;
        foreach (var rowElement in root.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowIndex++;

            if (!string.Equals(rowElement.Name.LocalName, "row", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.InvalidRowElement, "XML rows root can only contain 'row' elements."));
            }

            var children = rowElement.Elements().ToList();
            var isCellRow = children.All(e => string.Equals(e.Name.LocalName, "cell", StringComparison.Ordinal));

            if (isCellRow)
            {
                if (_options.RowSchemaMode == XmlImportRowSchemaMode.ObjectElementsOnly)
                {
                    throw new InvalidOperationException(
                        ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.SchemaModeViolation, $"XML row {rowIndex} uses 'cell' elements but RowSchemaMode is ObjectElementsOnly."));
                }

                if (mode == XmlRowMode.ObjectElements)
                {
                    throw new InvalidOperationException(
                        ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MixedRowSchemas, $"XML row {rowIndex} uses 'cell' elements but prior rows used object elements."));
                }

                mode = XmlRowMode.Cells;
                var row = new List<string>(children.Count);
                foreach (var cellElement in children)
                {
                    row.Add(cellElement.Value);
                }

                cellRows.Add(row);
                continue;
            }

            if (_options.RowSchemaMode == XmlImportRowSchemaMode.CellsOnly)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.SchemaModeViolation, $"XML row {rowIndex} uses object elements but RowSchemaMode is CellsOnly."));
            }

            if (!_options.EnableObjectElementRows)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.ObjectRowsDisabled, "XML object-element rows are disabled by configuration."));
            }

            if (mode == XmlRowMode.Cells)
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildMessage(SupportedExtensionValue, ContractDiagnostics.Operations.Import, ContractDiagnostics.Codes.MixedRowSchemas, $"XML row {rowIndex} uses object elements but prior rows used 'cell' elements."));
            }

            mode = XmlRowMode.ObjectElements;

            var rowValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in children)
            {
                var name = element.Name.LocalName;
                if (headerSet.Add(name))
                {
                    headers.Add(name);
                }

                rowValues[name] = element.Value;
            }

            objectRows.Add(rowValues);
        }

        List<IReadOnlyList<string>> results;
        if (mode == XmlRowMode.ObjectElements)
        {
            results = new List<IReadOnlyList<string>>(objectRows.Count + (_options.IncludeHeaderRowForObjectElementRows ? 1 : 0));
            if (_options.IncludeHeaderRowForObjectElementRows)
            {
                results.Add(headers);
            }

            foreach (var rowValues in objectRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = new List<string>(headers.Count);
                foreach (var header in headers)
                {
                    row.Add(rowValues.GetValueOrDefault(header, string.Empty));
                }

                results.Add(row);
            }
        }
        else
        {
            results = cellRows;
        }

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogImportCompleted(results.Count, elapsedMs);
        return results;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "XML import started")]
    private partial void LogImportStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "XML import completed with {RowCount} rows in {ElapsedMs}ms")]
    private partial void LogImportCompleted(int rowCount, double elapsedMs);

    private enum XmlRowMode
    {
        Unknown,
        Cells,
        ObjectElements,
    }

    private const string SupportedExtensionValue = ".xml";
}
