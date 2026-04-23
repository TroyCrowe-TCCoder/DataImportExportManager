namespace DataImportExportManager.Tests;

using System.Collections.Concurrent;
using DataImportExportManager.Extensions;
using DataImportExportManager.Exporters;
using DataImportExportManager.Importers;
using DataImportExportManager.Interfaces;
using DataImportExportManager.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void WhenRegisteredThenResolvesDeterministicRouterAndKnownFormats()
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();

        Assert.Equal(".csv", router.GetImporter("csv").SupportedExtension);
        Assert.Equal(".tsv", router.GetImporter("tsv").SupportedExtension);
        Assert.Equal(".xlsx", router.GetImporter(".XLSX").SupportedExtension);
        Assert.Equal(".json", router.GetImporter("JSON").SupportedExtension);
        Assert.Equal(".ndjson", router.GetImporter(".ndjson").SupportedExtension);
        Assert.Equal(".xml", router.GetImporter("xml").SupportedExtension);

        Assert.Equal(".csv", router.GetExporter(".csv").SupportedExtension);
        Assert.Equal(".tsv", router.GetExporter("TSV").SupportedExtension);
        Assert.Equal(".xlsx", router.GetExporter("xlsx").SupportedExtension);
        Assert.Equal(".json", router.GetExporter(".json").SupportedExtension);
        Assert.Equal(".ndjson", router.GetExporter("NDJSON").SupportedExtension);
        Assert.Equal(".xml", router.GetExporter(".xml").SupportedExtension);
    }

    [Fact]
    public void WhenConfigureCallbacksProvidedThenOptionsAreApplied()
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager(
            configureJsonImporter: options => options.IncludeHeaderRowForObjectRecords = false,
            configureJsonExporter: options => options.WriteIndented = true,
            configureNdjsonImporter: options => options.IgnoreBlankLines = false,
            configureNdjsonExporter: options => options.WriteTrailingNewline = false,
            configureXmlImporter: options =>
            {
                options.IncludeHeaderRowForObjectElementRows = false;
                options.RowSchemaMode = XmlImportRowSchemaMode.ObjectElementsOnly;
            },
            configureXmlExporter: options =>
            {
                options.UseObjectElementRows = true;
                options.StrictObjectElementRowWidth = true;
            });

        using var provider = services.BuildServiceProvider();

        var jsonImporterOptions = provider.GetRequiredService<JsonImporterOptions>();
        var jsonExporterOptions = provider.GetRequiredService<JsonExporterOptions>();
        var ndjsonImporterOptions = provider.GetRequiredService<NdjsonImporterOptions>();
        var ndjsonExporterOptions = provider.GetRequiredService<NdjsonExporterOptions>();
        var xmlImporterOptions = provider.GetRequiredService<XmlImporterOptions>();
        var xmlExporterOptions = provider.GetRequiredService<XmlExporterOptions>();

        Assert.False(jsonImporterOptions.IncludeHeaderRowForObjectRecords);
        Assert.True(jsonExporterOptions.WriteIndented);
        Assert.False(ndjsonImporterOptions.IgnoreBlankLines);
        Assert.False(ndjsonExporterOptions.WriteTrailingNewline);
        Assert.False(xmlImporterOptions.IncludeHeaderRowForObjectElementRows);
        Assert.Equal(XmlImportRowSchemaMode.ObjectElementsOnly, xmlImporterOptions.RowSchemaMode);
        Assert.True(xmlExporterOptions.UseObjectElementRows);
        Assert.True(xmlExporterOptions.StrictObjectElementRowWidth);
    }

    [Fact]
    public void WhenImportSchemaSessionCacheRegisteredThenResolvesCacheService()
    {
        var services = new ServiceCollection();
        services.AddImportSchemaSessionCache(TimeSpan.FromMinutes(5));
        using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<IImportSchemaSessionCache>();

        Assert.IsType<InMemoryImportSchemaSessionCache>(cache);
    }

    [Fact]
    public void WhenDistributedImportSchemaSessionCacheRegisteredThenResolvesDistributedCacheService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache, NoOpDistributedCache>();
        services.AddDistributedImportSchemaSessionCache(TimeSpan.FromMinutes(5));
        using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<IImportSchemaSessionCache>();

        Assert.IsType<DistributedImportSchemaSessionCache>(cache);
    }

    [Fact]
    public async Task WhenDistributedImportSchemaSessionCacheConfiguredThenAppliesKeyPrefixIsolation()
    {
        var services = new ServiceCollection();
        var backend = new NoOpDistributedCache();
        services.AddSingleton<IDistributedCache>(backend);
        services.AddDistributedImportSchemaSessionCache(
            defaultTtl: TimeSpan.FromMinutes(5),
            configureOptions: options =>
            {
                options.KeyPrefix = "env-a";
                options.MaxPayloadBytes = 8 * 1024;
            });

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IImportSchemaSessionCache>();

        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new DataImportExportManager.Contracts.TabularImportResult(["Id"], [["Id"]]));
        var session = await cache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.NotNull(session);
        Assert.NotNull(backend.LastSetKey);
        Assert.StartsWith("env-a::", backend.LastSetKey, StringComparison.Ordinal);
    }

    private sealed class NoOpDistributedCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

        public string? LastSetKey { get; private set; }

        public byte[]? Get(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return _entries.TryGetValue(key, out var payload) ? payload.ToArray() : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Get(key));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(options);

            LastSetKey = key;
            _entries[key] = value.ToArray();
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public void Remove(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            _entries.TryRemove(key, out _);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;
    }
}
