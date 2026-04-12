namespace DataImportExportManager.Importers;

/// <summary>
/// Configuration options for <see cref="XmlImporter"/>.
/// </summary>
public sealed class XmlImporterOptions
{
    /// <summary>
    /// Gets or sets the row schema mode for XML import.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="XmlImportRowSchemaMode.Auto"/>.
    /// </remarks>
    public XmlImportRowSchemaMode RowSchemaMode { get; set; } = XmlImportRowSchemaMode.Auto;

    /// <summary>
    /// Gets or sets a value indicating whether object-element row schemas should be imported.
    /// </summary>
    /// <remarks>
    /// When enabled, rows shaped like <c>&lt;row&gt;&lt;name&gt;Ada&lt;/name&gt;&lt;/row&gt;</c>
    /// are mapped deterministically to header + value rows.
    /// </remarks>
    public bool EnableObjectElementRows { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether synthesized headers should be included
    /// when importing object-element rows.
    /// </summary>
    public bool IncludeHeaderRowForObjectElementRows { get; set; } = true;
}

/// <summary>
/// Defines supported XML row schema modes for import.
/// </summary>
public enum XmlImportRowSchemaMode
{
    /// <summary>
    /// Automatically detect row schema, but do not allow mixed schemas in one document.
    /// </summary>
    Auto,

    /// <summary>
    /// Require all rows to use <c>&lt;cell&gt;</c> elements.
    /// </summary>
    CellsOnly,

    /// <summary>
    /// Require all rows to use object-element fields (for example <c>&lt;name&gt;</c>).
    /// </summary>
    ObjectElementsOnly,
}
