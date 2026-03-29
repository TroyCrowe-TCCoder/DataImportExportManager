namespace DataImportExportManager.Exporters;

/// <summary>
/// Configuration options for <see cref="ExcelExporter"/>.
/// </summary>
public sealed class ExcelExporterOptions
{
    /// <summary>
    /// Gets or sets the name of the worksheet that will be created in the exported workbook.
    /// </summary>
    /// <value>Defaults to <c>"Sheet1"</c>.</value>
    public string SheetName { get; set; } = "Sheet1";
}
