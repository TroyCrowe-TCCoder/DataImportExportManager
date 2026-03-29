namespace DataImportExportManager.Interfaces;

/// <summary>
/// Defines a contract for writing tabular data to a stream in a specific file format.
/// Each implementation handles a single format, adhering to the Interface Segregation Principle.
/// </summary>
public interface IDataExporter
{
    /// <summary>
    /// Gets the file extension this exporter produces (e.g., ".csv", ".xlsx").
    /// </summary>
    string SupportedExtension { get; }

    /// <summary>
    /// Asynchronously exports tabular data to the provided stream.
    /// </summary>
    /// <param name="data">
    /// The tabular data to export, represented as a read-only list of rows
    /// where each row is a read-only list of cell values.
    /// </param>
    /// <param name="destination">The stream to write the exported data to. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> or <paramref name="destination"/> is <see langword="null"/>.
    /// </exception>
    ValueTask ExportAsync(IReadOnlyList<IReadOnlyList<string>> data, Stream destination, CancellationToken cancellationToken = default);
}
