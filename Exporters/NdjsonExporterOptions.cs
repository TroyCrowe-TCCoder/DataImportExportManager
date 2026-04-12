namespace DataImportExportManager.Exporters;

/// <summary>
/// Configuration options for <see cref="NdjsonExporter"/>.
/// </summary>
public sealed class NdjsonExporterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether a final trailing newline should be written.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> for conventional NDJSON compatibility.
    /// </remarks>
    public bool WriteTrailingNewline { get; set; } = true;
}
