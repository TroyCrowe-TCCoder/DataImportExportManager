namespace DataImportExportManager.Importers;

using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Imports tabular data from TSV streams using tab as the delimiter.
/// </summary>
public sealed class TsvImporter : IDataImporter
{
    private readonly CsvImporter _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="TsvImporter"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for structured diagnostics.</param>
    public TsvImporter(ILogger<CsvImporter>? logger = null)
    {
        _inner = new CsvImporter(new CsvImporterOptions { Delimiter = '\t' }, logger);
    }

    /// <inheritdoc />
    public string SupportedExtension => ".tsv";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
        => _inner.ImportAsync(source, cancellationToken);
}
