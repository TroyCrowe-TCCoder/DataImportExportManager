namespace DataImportExportManager.Exporters;

using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Exports tabular data to TSV streams using tab as the delimiter.
/// </summary>
public sealed class TsvExporter : IDataExporter
{
    private readonly CsvExporter _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="TsvExporter"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for structured diagnostics.</param>
    public TsvExporter(ILogger<CsvExporter>? logger = null)
    {
        _inner = new CsvExporter(new CsvExporterOptions { Delimiter = '\t' }, logger);
    }

    /// <inheritdoc />
    public string SupportedExtension => ".tsv";

    /// <inheritdoc />
    public ValueTask ExportAsync(
        IReadOnlyList<IReadOnlyList<string>> data,
        Stream destination,
        CancellationToken cancellationToken = default)
        => _inner.ExportAsync(data, destination, cancellationToken);
}
