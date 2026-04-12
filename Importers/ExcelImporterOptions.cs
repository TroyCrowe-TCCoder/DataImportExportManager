namespace DataImportExportManager.Importers;

/// <summary>
/// Configuration options for <see cref="ExcelImporter"/>.
/// </summary>
public sealed class ExcelImporterOptions
{
    /// <summary>
    /// Gets or sets the maximum stream size (in bytes) that will be buffered
    /// into memory when the source stream is not seekable.
    /// </summary>
    /// <remarks>
    /// Choose a value appropriate for your hosting environment's memory constraints.
    /// For example, Azure App Service Basic tier has 1.75 GB RAM shared across all
    /// requests — a lower value (e.g., 25 MB) may be appropriate under concurrent load.
    /// </remarks>
    /// <value>Defaults to 100 MB (104,857,600 bytes).</value>
    public long MaxBufferSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the name of the worksheet to import.
    /// When <see langword="null"/> and <see cref="SheetIndex"/> is also <see langword="null"/>,
    /// the first worksheet is used. Takes precedence over <see cref="SheetIndex"/> when both are set.
    /// </summary>
    public string? SheetName { get; set; }

    /// <summary>
    /// Gets or sets the zero-based index of the worksheet to import.
    /// When <see langword="null"/> and <see cref="SheetName"/> is also <see langword="null"/>,
    /// the first worksheet (index 0) is used.
    /// <see cref="SheetName"/> takes precedence when both are set.
    /// </summary>
    public int? SheetIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether hidden/control characters should be normalized.
    /// When enabled, zero-width format characters and non-tab/newline control characters are removed,
    /// and non-breaking spaces are converted to regular spaces.
    /// </summary>
    /// <remarks>Defaults to <see langword="false"/> to preserve raw imported values.</remarks>
    public bool NormalizeHiddenCharacters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether date-formatted numeric cells should be interpreted
    /// as ISO 8601 date/time strings.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, numeric values are returned exactly as stored.
    /// Defaults to <see langword="false"/>.
    /// </remarks>
    public bool ParseDateFormattedCells { get; set; }
}
