namespace DataImportExportManager.Services;

using DataImportExportManager.Diagnostics;
using DataImportExportManager.Interfaces;

/// <summary>
/// Routes import and export operations to registered handlers using normalized file extensions.
/// </summary>
public sealed class DataFormatRouter : IDataFormatRouter
{
    private readonly Dictionary<string, IDataImporter> _importers;
    private readonly Dictionary<string, IDataExporter> _exporters;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataFormatRouter"/> class.
    /// </summary>
    /// <param name="importers">Registered importers.</param>
    /// <param name="exporters">Registered exporters.</param>
    public DataFormatRouter(IEnumerable<IDataImporter> importers, IEnumerable<IDataExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(importers);
        ArgumentNullException.ThrowIfNull(exporters);

        _importers = BuildImporterMap(importers);
        _exporters = BuildExporterMap(exporters);
    }

    /// <inheritdoc />
    public IDataImporter GetImporter(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return _importers.TryGetValue(normalized, out var importer)
            ? importer
            : throw new InvalidOperationException(
                ContractDiagnostics.BuildImportMessage(normalized, ContractDiagnostics.Codes.RouteNotFound, $"No importer is registered for extension '{normalized}'."));
    }

    /// <inheritdoc />
    public IDataExporter GetExporter(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return _exporters.TryGetValue(normalized, out var exporter)
            ? exporter
            : throw new InvalidOperationException(
                ContractDiagnostics.BuildExportMessage(normalized, ContractDiagnostics.Codes.RouteNotFound, $"No exporter is registered for extension '{normalized}'."));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
        string extension,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalized = NormalizeExtension(extension);
        var importer = GetImporter(normalized);

        return ExecuteImportAsync(importer, normalized, source, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask ExportAsync(
        string extension,
        IReadOnlyList<IReadOnlyList<string>> data,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(destination);

        var normalized = NormalizeExtension(extension);
        var exporter = GetExporter(normalized);

        return ExecuteExportAsync(exporter, normalized, data, destination, cancellationToken);
    }

    private static async ValueTask<IReadOnlyList<IReadOnlyList<string>>> ExecuteImportAsync(
        IDataImporter importer,
        string normalizedExtension,
        Stream source,
        CancellationToken cancellationToken)
    {
        try
        {
            return await importer.ImportAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (!HasDiagnosticPrefix(ex.Message))
        {
            throw new InvalidOperationException(
                ContractDiagnostics.BuildImportMessage(normalizedExtension, ContractDiagnostics.Codes.Contract, ex.Message),
                ex);
        }
    }

    private static async ValueTask ExecuteExportAsync(
        IDataExporter exporter,
        string normalizedExtension,
        IReadOnlyList<IReadOnlyList<string>> data,
        Stream destination,
        CancellationToken cancellationToken)
    {
        try
        {
            await exporter.ExportAsync(data, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (!HasDiagnosticPrefix(ex.Message))
        {
            throw new InvalidOperationException(
                ContractDiagnostics.BuildExportMessage(normalizedExtension, ContractDiagnostics.Codes.Contract, ex.Message),
                ex);
        }
    }

    private static Dictionary<string, IDataImporter> BuildImporterMap(IEnumerable<IDataImporter> importers)
    {
        var map = new Dictionary<string, IDataImporter>(StringComparer.OrdinalIgnoreCase);

        foreach (var importer in importers)
        {
            ArgumentNullException.ThrowIfNull(importer);

            var normalized = NormalizeExtension(importer.SupportedExtension);
            if (!map.TryAdd(normalized, importer))
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildImportMessage(normalized, ContractDiagnostics.Codes.DuplicateHandler, $"Multiple importers are registered for extension '{normalized}'."));
            }
        }

        return map;
    }

    private static Dictionary<string, IDataExporter> BuildExporterMap(IEnumerable<IDataExporter> exporters)
    {
        var map = new Dictionary<string, IDataExporter>(StringComparer.OrdinalIgnoreCase);

        foreach (var exporter in exporters)
        {
            ArgumentNullException.ThrowIfNull(exporter);

            var normalized = NormalizeExtension(exporter.SupportedExtension);
            if (!map.TryAdd(normalized, exporter))
            {
                throw new InvalidOperationException(
                    ContractDiagnostics.BuildExportMessage(normalized, ContractDiagnostics.Codes.DuplicateHandler, $"Multiple exporters are registered for extension '{normalized}'."));
            }
        }

        return map;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Extension cannot be null, empty, or whitespace.", nameof(extension));
        }

        var trimmed = extension.Trim();
        var normalized = trimmed[0] == '.' ? trimmed : $".{trimmed}";
        return normalized.Equals(".jsonl", StringComparison.OrdinalIgnoreCase)
            ? ".ndjson"
            : normalized;
    }

    private static bool HasDiagnosticPrefix(string message)
        => ContractDiagnostics.HasPrefix(message);
}
