namespace DataImportExportManager.Tests;

using System.Collections.Concurrent;
using DataImportExportManager.Contracts;
using DataImportExportManager.Services;
using Microsoft.Extensions.Caching.Distributed;

public class SessionCachePerformanceTests
{
    [Fact]
    public async Task WhenInMemorySessionCacheRunsBaselineThenStoreGetConsumeStaysWithinThreshold()
    {
        var cache = new InMemoryImportSchemaSessionCache();
        var importResult = new TabularImportResult(["Id", "Name"], [["Id", "Name"], ["1", "Ada"]]);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < SessionCacheBenchmarkBaselines.IterationCount; i++)
        {
            var tenantId = $"tenant-{i % 10}";
            var subjectId = $"subject-{i % 25}";

            var sessionId = await cache.StoreAsync(tenantId, subjectId, importResult);
            var session = await cache.TryGetAsync(tenantId, subjectId, sessionId);
            var consumed = await cache.ConsumeAsync(tenantId, subjectId, sessionId);

            Assert.NotNull(session);
            Assert.NotNull(consumed);
        }

        stopwatch.Stop();

        Assert.True(
            stopwatch.ElapsedMilliseconds <= SessionCacheBenchmarkBaselines.InMemoryStoreGetConsumeMaxMilliseconds,
            $"In-memory session cache baseline exceeded threshold. Elapsed={stopwatch.ElapsedMilliseconds}ms, Threshold={SessionCacheBenchmarkBaselines.InMemoryStoreGetConsumeMaxMilliseconds}ms.");
    }

    [Fact]
    public async Task WhenDistributedSessionCacheRunsBaselineThenStoreGetConsumeStaysWithinThreshold()
    {
        var backend = new MemoryDistributedCache();
        var cache = new DistributedImportSchemaSessionCache(backend);
        var importResult = new TabularImportResult(["Id", "Name"], [["Id", "Name"], ["1", "Ada"]]);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < SessionCacheBenchmarkBaselines.IterationCount; i++)
        {
            var tenantId = $"tenant-{i % 10}";
            var subjectId = $"subject-{i % 25}";

            var sessionId = await cache.StoreAsync(tenantId, subjectId, importResult);
            var session = await cache.TryGetAsync(tenantId, subjectId, sessionId);
            var consumed = await cache.ConsumeAsync(tenantId, subjectId, sessionId);

            Assert.NotNull(session);
            Assert.NotNull(consumed);
        }

        stopwatch.Stop();

        Assert.True(
            stopwatch.ElapsedMilliseconds <= SessionCacheBenchmarkBaselines.DistributedStoreGetConsumeMaxMilliseconds,
            $"Distributed session cache baseline exceeded threshold. Elapsed={stopwatch.ElapsedMilliseconds}ms, Threshold={SessionCacheBenchmarkBaselines.DistributedStoreGetConsumeMaxMilliseconds}ms.");
    }

    private sealed class MemoryDistributedCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

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
            ArgumentNullException.ThrowIfNull(key);
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            _entries.TryRemove(key, out _);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            Remove(key);
            return Task.CompletedTask;
        }
    }
}
