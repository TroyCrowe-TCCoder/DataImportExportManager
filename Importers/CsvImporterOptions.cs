namespace DataImportExportManager.Importers;

using System.Text;

/// <summary>
/// Configuration options for <see cref="CsvImporter"/>.
/// </summary>
public sealed class CsvImporterOptions
{
    /// <summary>
    /// Gets or sets the text encoding used to read the CSV stream.
    /// Defaults to <see cref="Encoding.UTF8"/>.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Gets or sets the character used as a field delimiter.
    /// Defaults to comma (<c>','</c>).
    /// </summary>
    public char Delimiter { get; set; } = ',';
}
