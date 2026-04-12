namespace DataImportExportManager.Exporters;

/// <summary>
/// Configuration options for <see cref="JsonExporter"/>.
/// </summary>
public sealed class JsonExporterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether exported JSON should be indented.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/> for compact payloads.
    /// </remarks>
    public bool WriteIndented { get; set; }
}
