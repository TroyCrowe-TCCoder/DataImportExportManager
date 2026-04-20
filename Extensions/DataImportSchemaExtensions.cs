namespace DataImportExportManager.Extensions;

using DataImportExportManager.Contracts;
using DataImportExportManager.Interfaces;

/// <summary>
/// Provides helpers to extract column names from imported tabular rows.
/// </summary>
public static class DataImportSchemaExtensions
{
    /// <summary>
    /// Imports tabular data once and returns both rich and tuple-shaped schema results.
    /// </summary>
    /// <param name="importer">The importer to execute.</param>
    /// <param name="source">The source stream containing import data.</param>
    /// <param name="cancellationToken">A cancellation token for the import operation.</param>
    /// <returns>
    /// A tuple containing the full <see cref="TabularImportResult"/> and a tuple-shaped
    /// projection of <c>Headers</c> and <c>DataRows</c> for lightweight consumers.
    /// </returns>
    public static async ValueTask<(TabularImportResult Result, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> DataRows)> ImportWithSchemaBundleAsync(
        this IDataImporter importer,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var result = await importer.ImportWithSchemaAsync(source, cancellationToken).ConfigureAwait(false);
        return (result, result.Columns, result.DataRows);
    }

    /// <summary>
    /// Imports tabular data once via format routing and returns both rich and tuple-shaped schema results.
    /// </summary>
    /// <param name="router">The format router used to select the importer.</param>
    /// <param name="extension">The source file extension identifying the importer.</param>
    /// <param name="source">The source stream containing import data.</param>
    /// <param name="cancellationToken">A cancellation token for the import operation.</param>
    /// <returns>
    /// A tuple containing the full <see cref="TabularImportResult"/> and a tuple-shaped
    /// projection of <c>Headers</c> and <c>DataRows</c> for lightweight consumers.
    /// </returns>
    public static async ValueTask<(TabularImportResult Result, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> DataRows)> ImportWithSchemaBundleAsync(
        this IDataFormatRouter router,
        string extension,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var result = await router.ImportWithSchemaAsync(extension, source, cancellationToken).ConfigureAwait(false);
        return (result, result.Columns, result.DataRows);
    }

    /// <summary>
    /// Imports tabular data and returns a tuple split into header fields and data rows.
    /// </summary>
    /// <param name="importer">The importer to execute.</param>
    /// <param name="source">The source stream containing import data.</param>
    /// <param name="cancellationToken">A cancellation token for the import operation.</param>
    /// <returns>
    /// A tuple where <c>Headers</c> is populated from the first imported row and
    /// <c>DataRows</c> contains the remaining imported rows.
    /// </returns>
    public static async ValueTask<(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> DataRows)> ImportWithSchemaTupleAsync(
        this IDataImporter importer,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var result = await importer.ImportWithSchemaAsync(source, cancellationToken).ConfigureAwait(false);
        return (result.Columns, result.DataRows);
    }

    /// <summary>
    /// Imports tabular data via format routing and returns a tuple split into header fields and data rows.
    /// </summary>
    /// <param name="router">The format router used to select the importer.</param>
    /// <param name="extension">The source file extension identifying the importer.</param>
    /// <param name="source">The source stream containing import data.</param>
    /// <param name="cancellationToken">A cancellation token for the import operation.</param>
    /// <returns>
    /// A tuple where <c>Headers</c> is populated from the first imported row and
    /// <c>DataRows</c> contains the remaining imported rows.
    /// </returns>
    public static async ValueTask<(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> DataRows)> ImportWithSchemaTupleAsync(
        this IDataFormatRouter router,
        string extension,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var result = await router.ImportWithSchemaAsync(extension, source, cancellationToken).ConfigureAwait(false);
        return (result.Columns, result.DataRows);
    }

    /// <summary>
    /// Imports tabular data and extracts the first row as column names.
    /// </summary>
    /// <param name="importer">The importer to execute.</param>
    /// <param name="source">The source stream containing import data.</param>
    /// <param name="cancellationToken">A cancellation token for the import operation.</param>
    /// <returns>
    /// A <see cref="TabularImportResult"/> where <see cref="TabularImportResult.Columns"/> is
    /// populated from the first imported row and <see cref="TabularImportResult.Rows"/> contains
    /// the full imported rows including that header row.
    /// </returns>
    public static async ValueTask<TabularImportResult> ImportWithSchemaAsync(
        this IDataImporter importer,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentNullException.ThrowIfNull(source);

        var importedRows = await importer.ImportAsync(source, cancellationToken).ConfigureAwait(false);
        return CreateResult(importedRows);
    }

    /// <summary>
    /// Imports tabular data via format routing and extracts the first row as column names.
    /// </summary>
    /// <param name="router">The format router used to select the importer.</param>
    /// <param name="extension">The source file extension identifying the importer.</param>
    /// <param name="source">The source stream containing import data.</param>
    /// <param name="cancellationToken">A cancellation token for the import operation.</param>
    /// <returns>
    /// A <see cref="TabularImportResult"/> where <see cref="TabularImportResult.Columns"/> is
    /// populated from the first imported row and <see cref="TabularImportResult.Rows"/> contains
    /// the full imported rows including that header row.
    /// </returns>
    public static async ValueTask<TabularImportResult> ImportWithSchemaAsync(
        this IDataFormatRouter router,
        string extension,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(source);

        var importedRows = await router.ImportAsync(extension, source, cancellationToken).ConfigureAwait(false);
        return CreateResult(importedRows);
    }

    /// <summary>
    /// Creates an example import file for a given format using the router-selected exporter.
    /// </summary>
    /// <param name="router">The format router used to select the exporter.</param>
    /// <param name="extension">The destination file extension identifying the exporter.</param>
    /// <param name="headers">The header fields to include in the template.</param>
    /// <param name="destination">The destination stream for the generated example file.</param>
    /// <param name="sampleRow">
    /// Optional example row to include after the header. When <see langword="null"/>, a deterministic
    /// sample row is generated from the header names (for example, <c>sample_CustomerId</c>).
    /// </param>
    /// <param name="cancellationToken">A cancellation token for the export operation.</param>
    /// <returns>A task representing the asynchronous export operation.</returns>
    public static ValueTask CreateExampleImportFileAsync(
        this IDataFormatRouter router,
        string extension,
        IReadOnlyList<string> headers,
        Stream destination,
        IReadOnlyList<string>? sampleRow = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(destination);

        if (headers.Count == 0)
        {
            throw new ArgumentException("At least one header field is required.", nameof(headers));
        }

        for (var i = 0; i < headers.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(headers[i]))
            {
                throw new ArgumentException("Header fields cannot be null, empty, or whitespace.", nameof(headers));
            }
        }

        var resolvedSampleRow = sampleRow ?? CreateDeterministicSampleRow(headers);
        if (resolvedSampleRow.Count != headers.Count)
        {
            throw new ArgumentException("Sample row field count must match header field count.", nameof(sampleRow));
        }

        IReadOnlyList<IReadOnlyList<string>> rows = [headers.ToArray(), resolvedSampleRow.ToArray()];
        return router.ExportAsync(extension, rows, destination, cancellationToken);
    }

    private static TabularImportResult CreateResult(IReadOnlyList<IReadOnlyList<string>> importedRows)
    {
        if (importedRows.Count == 0)
        {
            return TabularImportResult.Empty;
        }

        var columns = importedRows[0].ToArray();
        var rows = importedRows.ToArray();
        return new TabularImportResult(columns, rows);
    }

    private static string[] CreateDeterministicSampleRow(IReadOnlyList<string> headers)
    {
        var sampleRow = new string[headers.Count];
        for (var i = 0; i < headers.Count; i++)
        {
            sampleRow[i] = $"sample_{headers[i].Trim()}";
        }

        return sampleRow;
    }
}
