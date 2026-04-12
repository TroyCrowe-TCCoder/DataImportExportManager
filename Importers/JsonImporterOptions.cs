namespace DataImportExportManager.Importers;

/// <summary>
/// Configuration options for <see cref="JsonImporter"/>.
/// </summary>
public sealed class JsonImporterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether object-record JSON payloads should emit
    /// a synthesized header row as the first result row.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> for deterministic tabular output.
    /// </remarks>
    public bool IncludeHeaderRowForObjectRecords { get; set; } = true;
}
