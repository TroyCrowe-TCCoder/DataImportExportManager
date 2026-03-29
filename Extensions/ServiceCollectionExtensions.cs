namespace DataImportExportManager.Extensions;

using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register
/// DataImportExportManager services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all DataImportExportManager importers and exporters with the
    /// dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureExcelImporter">
    /// Optional callback to configure <see cref="ExcelImporterOptions"/> (e.g., buffer
    /// size limits appropriate for the hosting environment's memory constraints).
    /// </param>
    /// <param name="configureExcelExporter">
    /// Optional callback to configure <see cref="ExcelExporterOptions"/> (e.g., a custom
    /// worksheet name for the exported workbook).
    /// </param>
    /// <param name="configureCsvImporter">
    /// Optional callback to configure <see cref="CsvImporterOptions"/> (e.g., custom
    /// delimiter or encoding for non-standard CSV files).
    /// </param>
    /// <param name="configureCsvExporter">
    /// Optional callback to configure <see cref="CsvExporterOptions"/> (e.g., enabling
    /// formula-cell sanitization for consumer-facing exports).
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// All services are registered as singletons. The library depends only on
    /// <c>ILogger&lt;T&gt;</c> from <c>Microsoft.Extensions.Logging.Abstractions</c> —
    /// the consuming application is responsible for configuring its own logging pipeline
    /// (e.g., console, Application Insights, Seq) via <c>AddLogging()</c>.
    /// </remarks>
    public static IServiceCollection AddDataImportExportManager(
        this IServiceCollection services,
        Action<ExcelImporterOptions>? configureExcelImporter = null,
        Action<ExcelExporterOptions>? configureExcelExporter = null,
        Action<CsvImporterOptions>? configureCsvImporter = null,
        Action<CsvExporterOptions>? configureCsvExporter = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var excelImporterOptions = new ExcelImporterOptions();
        configureExcelImporter?.Invoke(excelImporterOptions);
        services.AddSingleton(excelImporterOptions);

        var excelExporterOptions = new ExcelExporterOptions();
        configureExcelExporter?.Invoke(excelExporterOptions);
        services.AddSingleton(excelExporterOptions);

        var csvImporterOptions = new CsvImporterOptions();
        configureCsvImporter?.Invoke(csvImporterOptions);
        services.AddSingleton(csvImporterOptions);

        var csvExporterOptions = new CsvExporterOptions();
        configureCsvExporter?.Invoke(csvExporterOptions);
        services.AddSingleton(csvExporterOptions);

        services.AddSingleton<IDataImporter, CsvImporter>();
        services.AddSingleton<IDataImporter, ExcelImporter>();
        services.AddSingleton<IDataExporter, CsvExporter>();
        services.AddSingleton<IDataExporter, ExcelExporter>();

        return services;
    }
}

