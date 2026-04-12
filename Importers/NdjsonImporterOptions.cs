namespace DataImportExportManager.Importers;

/// <summary>
/// Configuration options for <see cref="NdjsonImporter"/>.
/// </summary>
public sealed class NdjsonImporterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether blank or whitespace-only lines should be ignored.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> for resilient ingestion.
    /// </remarks>
    public bool IgnoreBlankLines { get; set; } = true;
}
