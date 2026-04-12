namespace DataImportExportManager.Exporters;

/// <summary>
/// Configuration options for <see cref="XmlExporter"/>.
/// </summary>
public sealed class XmlExporterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether export should use object-element rows.
    /// </summary>
    /// <remarks>
    /// When enabled, the first row is treated as header names and subsequent rows are emitted
    /// as <c>&lt;row&gt;&lt;header&gt;value&lt;/header&gt;&lt;/row&gt;</c>. Defaults to <see langword="false"/>.
    /// </remarks>
    public bool UseObjectElementRows { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether object-element export should enforce
    /// strict row-width contract matching the header count.
    /// </summary>
    /// <remarks>
    /// When enabled and <see cref="UseObjectElementRows"/> is <see langword="true"/>,
    /// each data row must have the same number of columns as the header row.
    /// </remarks>
    public bool StrictObjectElementRowWidth { get; set; }
}
