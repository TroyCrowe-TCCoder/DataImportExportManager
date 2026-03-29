namespace DataImportExportManager.Interfaces;

/// <summary>
/// Defines a contract for reading tabular data from a stream in a specific file format.
/// Each implementation handles a single format, adhering to the Interface Segregation Principle.
/// </summary>
public interface IDataImporter
{
    /// <summary>
    /// Gets the file extension this importer handles (e.g., ".csv", ".xlsx").
    /// </summary>
    string SupportedExtension { get; }

    /// <summary>
    /// Asynchronously imports tabular data from the provided stream.
    /// </summary>
    /// <param name="source">The stream containing the data to import. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A read-only list of rows, where each row is a read-only list of cell values as strings.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(Stream source, CancellationToken cancellationToken = default);
}
