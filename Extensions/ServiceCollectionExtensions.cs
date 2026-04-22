namespace DataImportExportManager.Extensions;

using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;
using DataImportExportManager.Interfaces;
using DataImportExportManager.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register
/// DataImportExportManager services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory import schema session cache used for temporary no-reupload workflows.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="defaultTtl">Optional default TTL for cached sessions.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddImportSchemaSessionCache(
        this IServiceCollection services,
        TimeSpan? defaultTtl = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IImportSchemaSessionCache>(_ => new InMemoryImportSchemaSessionCache(defaultTtl));
        return services;
    }

    /// <summary>
    /// Registers the distributed import schema session cache for multi-instance no-reupload workflows.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="defaultTtl">Optional default TTL for cached sessions.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDistributedImportSchemaSessionCache(
        this IServiceCollection services,
        TimeSpan? defaultTtl = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IImportSchemaSessionCache>(provider =>
            new DistributedImportSchemaSessionCache(
                provider.GetRequiredService<IDistributedCache>(),
                defaultTtl));

        return services;
    }

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
    /// <param name="configureJsonImporter">
    /// Optional callback to configure <see cref="JsonImporterOptions"/>.
    /// </param>
    /// <param name="configureJsonExporter">
    /// Optional callback to configure <see cref="JsonExporterOptions"/>.
    /// </param>
    /// <param name="configureNdjsonImporter">
    /// Optional callback to configure <see cref="NdjsonImporterOptions"/>.
    /// </param>
    /// <param name="configureNdjsonExporter">
    /// Optional callback to configure <see cref="NdjsonExporterOptions"/>.
    /// </param>
    /// <param name="configureXmlImporter">
    /// Optional callback to configure <see cref="XmlImporterOptions"/>.
    /// </param>
    /// <param name="configureXmlExporter">
    /// Optional callback to configure <see cref="XmlExporterOptions"/>.
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
        Action<CsvExporterOptions>? configureCsvExporter = null,
        Action<JsonImporterOptions>? configureJsonImporter = null,
        Action<JsonExporterOptions>? configureJsonExporter = null,
        Action<NdjsonImporterOptions>? configureNdjsonImporter = null,
        Action<NdjsonExporterOptions>? configureNdjsonExporter = null,
        Action<XmlImporterOptions>? configureXmlImporter = null,
        Action<XmlExporterOptions>? configureXmlExporter = null)
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

        var jsonImporterOptions = new JsonImporterOptions();
        configureJsonImporter?.Invoke(jsonImporterOptions);
        services.AddSingleton(jsonImporterOptions);

        var jsonExporterOptions = new JsonExporterOptions();
        configureJsonExporter?.Invoke(jsonExporterOptions);
        services.AddSingleton(jsonExporterOptions);

        var ndjsonImporterOptions = new NdjsonImporterOptions();
        configureNdjsonImporter?.Invoke(ndjsonImporterOptions);
        services.AddSingleton(ndjsonImporterOptions);

        var ndjsonExporterOptions = new NdjsonExporterOptions();
        configureNdjsonExporter?.Invoke(ndjsonExporterOptions);
        services.AddSingleton(ndjsonExporterOptions);

        var xmlImporterOptions = new XmlImporterOptions();
        configureXmlImporter?.Invoke(xmlImporterOptions);
        services.AddSingleton(xmlImporterOptions);

        var xmlExporterOptions = new XmlExporterOptions();
        configureXmlExporter?.Invoke(xmlExporterOptions);
        services.AddSingleton(xmlExporterOptions);

        services.AddSingleton<IDataImporter, CsvImporter>();
        services.AddSingleton<IDataImporter, TsvImporter>();
        services.AddSingleton<IDataImporter, ExcelImporter>();
        services.AddSingleton<IDataImporter, JsonImporter>();
        services.AddSingleton<IDataImporter, NdjsonImporter>();
        services.AddSingleton<IDataImporter, XmlImporter>();
        services.AddSingleton<IDataExporter, CsvExporter>();
        services.AddSingleton<IDataExporter, TsvExporter>();
        services.AddSingleton<IDataExporter, ExcelExporter>();
        services.AddSingleton<IDataExporter, JsonExporter>();
        services.AddSingleton<IDataExporter, NdjsonExporter>();
        services.AddSingleton<IDataExporter, XmlExporter>();
        services.AddSingleton<IDataFormatRouter, DataFormatRouter>();

        return services;
    }
}

