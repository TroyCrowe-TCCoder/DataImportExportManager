namespace DataImportExportManager.Exporters;

using System.Text;

/// <summary>
/// Configuration options for <see cref="CsvExporter"/>.
/// </summary>
public sealed class CsvExporterOptions
{
    /// <summary>
    /// Gets or sets the text encoding used to write the CSV stream.
    /// Defaults to UTF-8 without a byte-order mark (BOM), matching the behaviour
    /// of most CSV consumers and the <see cref="System.IO.StreamWriter"/> default.
    /// </summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Gets or sets the character used as a field delimiter.
    /// Defaults to comma (<c>','</c>).
    /// </summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>
    /// Gets or sets a value indicating whether cell values starting with
    /// formula-triggering characters (<c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>,
    /// tab, or carriage-return) are prefixed with a single quote to prevent
    /// spreadsheet formula injection.
    /// </summary>
    /// <remarks>
    /// Disabled by default so the library faithfully serialises exactly what the
    /// consumer provides. Enable this when the CSV output may be opened directly
    /// in a spreadsheet application (e.g., Excel, Google Sheets) and the data
    /// could contain user-supplied values.
    /// </remarks>
    public bool SanitizeFormulaCells { get; set; }
}

