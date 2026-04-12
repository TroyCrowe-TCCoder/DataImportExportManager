namespace DataImportExportManager.Interfaces;

/// <summary>
/// Selects registered data importers and exporters by file extension using deterministic rules.
/// </summary>
public interface IDataFormatRouter
{
    /// <summary>
    /// Returns the importer registered for the provided file extension.
    /// </summary>
    /// <param name="extension">The target file extension (for example, <c>.csv</c> or <c>xlsx</c>).</param>
    /// <returns>The matching importer.</returns>
    /// <exception cref="ArgumentException"><paramref name="extension"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No importer is registered for the normalized extension.</exception>
    IDataImporter GetImporter(string extension);

    /// <summary>
    /// Returns the exporter registered for the provided file extension.
    /// </summary>
    /// <param name="extension">The target file extension (for example, <c>.csv</c> or <c>xlsx</c>).</param>
    /// <returns>The matching exporter.</returns>
    /// <exception cref="ArgumentException"><paramref name="extension"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No exporter is registered for the normalized extension.</exception>
    IDataExporter GetExporter(string extension);

    /// <summary>
    /// Imports rows using the importer registered for the provided extension.
    /// </summary>
    /// <param name="extension">The file extension identifying which importer to use.</param>
    /// <param name="source">The source stream to import from.</param>
    /// <param name="cancellationToken">A cancellation token for the import operation.</param>
    /// <returns>The imported rows.</returns>
    ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        string extension,
        Stream source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports rows using the exporter registered for the provided extension.
    /// </summary>
    /// <param name="extension">The file extension identifying which exporter to use.</param>
    /// <param name="data">The rows to export.</param>
    /// <param name="destination">The destination stream to export to.</param>
    /// <param name="cancellationToken">A cancellation token for the export operation.</param>
    ValueTask ExportAsync(
        string extension,
        IReadOnlyList<IReadOnlyList<string>> data,
        Stream destination,
        CancellationToken cancellationToken = default);
}
