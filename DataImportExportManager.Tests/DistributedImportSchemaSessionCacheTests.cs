namespace DataImportExportManager.Tests;

using System.Collections.Concurrent;
using System.Text;
using DataImportExportManager.Contracts;
using DataImportExportManager.Interfaces;
using DataImportExportManager.Services;
using Microsoft.Extensions.Caching.Distributed;

public class DistributedImportSchemaSessionCacheTests
{
    [Fact]
    public async Task WhenPayloadExceedsConfiguredLimitThenStoreThrowsInvalidOperationException()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = new DistributedImportSchemaSessionCacheOptions
        {
            MaxPayloadBytes = 64,
        };

        var cache = new DistributedImportSchemaSessionCache(new FakeDistributedCache(clock), options: options, timeProvider: clock);
        var largeValue = new string('A', 200);
        var importResult = new TabularImportResult(["ColumnA"], [["ColumnA"], [largeValue]]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.StoreAsync("tenant-a", "user-a", importResult).AsTask());

        Assert.Equal("Session payload exceeds configured maximum size of 64 bytes.", ex.Message);
    }

    [Fact]
    public async Task WhenCustomKeyPrefixUsedThenDifferentPrefixCannotResolveSession()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var backend = new FakeDistributedCache(clock);
        var prefixedCache = new DistributedImportSchemaSessionCache(
            backend,
            options: new DistributedImportSchemaSessionCacheOptions { KeyPrefix = "env-a" },
            timeProvider: clock);
        var otherPrefixedCache = new DistributedImportSchemaSessionCache(
            backend,
            options: new DistributedImportSchemaSessionCacheOptions { KeyPrefix = "env-b" },
            timeProvider: clock);

        var sessionId = await prefixedCache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));
        var foundWithOwnerPrefix = await prefixedCache.TryGetAsync("tenant-a", "user-a", sessionId);
        var foundWithOtherPrefix = await otherPrefixedCache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.NotNull(foundWithOwnerPrefix);
        Assert.Null(foundWithOtherPrefix);
    }

    [Fact]
    public async Task WhenStoredThenTryGetReturnsSessionWithoutReupload()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var cacheBackend = new FakeDistributedCache(clock);
        var cache = new DistributedImportSchemaSessionCache(cacheBackend, timeProvider: clock);
        var importResult = new TabularImportResult(["Id"], [["Id"], ["1"]]);

        var sessionId = await cache.StoreAsync("tenant-a", "user-a", importResult);
        var session = await cache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.NotNull(session);
        Assert.Equal(importResult.Columns, session!.ImportResult.Columns);
        Assert.Equal(importResult.Rows.Count, session.ImportResult.Rows.Count);
        Assert.Equal(importResult.Rows[0], session.ImportResult.Rows[0]);
        Assert.Equal(importResult.Rows[1], session.ImportResult.Rows[1]);
    }

    [Fact]
    public async Task WhenStoredThenPublishesSessionStoredEvent()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var publisher = new RecordingEventPublisher();
        var cache = new DistributedImportSchemaSessionCache(new FakeDistributedCache(clock), timeProvider: clock, eventPublisher: publisher);

        _ = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        Assert.Single(publisher.Events);
        Assert.Equal(DataImportExportEventNames.SessionStored, publisher.Events[0].EventName);
        Assert.Equal("tenant-a", publisher.Events[0].TenantId);
        Assert.Equal("user-a", publisher.Events[0].SubjectId);
        Assert.Equal("Schema session stored.", publisher.Events[0].Message);
        Assert.False(string.IsNullOrWhiteSpace(publisher.Events[0].SessionId));
    }

    [Fact]
    public async Task WhenDifferentTenantOrSubjectThenTryGetReturnsNull()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var cache = new DistributedImportSchemaSessionCache(new FakeDistributedCache(clock), timeProvider: clock);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        var tenantMismatch = await cache.TryGetAsync("tenant-b", "user-a", sessionId);
        var subjectMismatch = await cache.TryGetAsync("tenant-a", "user-b", sessionId);

        Assert.Null(tenantMismatch);
        Assert.Null(subjectMismatch);
    }

    [Fact]
    public async Task WhenConsumedThenSessionReturnsOnceAndIsInvalidated()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var cache = new DistributedImportSchemaSessionCache(new FakeDistributedCache(clock), timeProvider: clock);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        var consumed = await cache.ConsumeAsync("tenant-a", "user-a", sessionId);
        var afterConsume = await cache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.NotNull(consumed);
        Assert.Null(afterConsume);
    }

    [Fact]
    public async Task WhenConsumedThenPublishesSessionConsumedEvent()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var publisher = new RecordingEventPublisher();
        var cache = new DistributedImportSchemaSessionCache(new FakeDistributedCache(clock), timeProvider: clock, eventPublisher: publisher);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        _ = await cache.ConsumeAsync("tenant-a", "user-a", sessionId);

        var consumedEvent = Assert.Single(publisher.Events, e => e.EventName == DataImportExportEventNames.SessionConsumed);
        Assert.Equal("tenant-a", consumedEvent.TenantId);
        Assert.Equal("user-a", consumedEvent.SubjectId);
        Assert.Equal(sessionId, consumedEvent.SessionId);
        Assert.Equal("Schema session consumed.", consumedEvent.Message);
    }

    [Fact]
    public async Task WhenExpiredThenTryGetAndConsumeReturnNull()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var cache = new DistributedImportSchemaSessionCache(new FakeDistributedCache(clock), defaultTtl: TimeSpan.FromMinutes(5), timeProvider: clock);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        clock.Advance(TimeSpan.FromMinutes(6));
        var retrieved = await cache.TryGetAsync("tenant-a", "user-a", sessionId);
        var consumed = await cache.ConsumeAsync("tenant-a", "user-a", sessionId);

        Assert.Null(retrieved);
        Assert.Null(consumed);
    }

    [Fact]
    public async Task WhenCancellationRequestedThenOperationsThrowOperationCanceledException()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var cache = new DistributedImportSchemaSessionCache(new FakeDistributedCache(clock), timeProvider: clock);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]), cancellationToken: cts.Token).AsTask());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.TryGetAsync("tenant-a", "user-a", sessionId, cts.Token).AsTask());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.RemoveAsync("tenant-a", "user-a", sessionId, cts.Token).AsTask());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.ConsumeAsync("tenant-a", "user-a", sessionId, cts.Token).AsTask());
    }

    [Fact]
    public void WhenOptionsKeyPrefixInvalidThenConstructorThrowsArgumentException()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            _ = new DistributedImportSchemaSessionCache(
                new FakeDistributedCache(clock),
                options: new DistributedImportSchemaSessionCacheOptions { KeyPrefix = "  " },
                timeProvider: clock));
    }

    [Fact]
    public void WhenOptionsMaxPayloadNotPositiveThenConstructorThrowsArgumentOutOfRangeException()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new DistributedImportSchemaSessionCache(
                new FakeDistributedCache(clock),
                options: new DistributedImportSchemaSessionCacheOptions { MaxPayloadBytes = 0 },
                timeProvider: clock));
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private sealed class FakeDistributedCache(TimeProvider timeProvider) : IDistributedCache
    {
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly ConcurrentDictionary<string, CacheRecord> _entries = new(StringComparer.Ordinal);

        public byte[]? Get(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return TryGetValue(key);
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

            var absoluteExpiration = options.AbsoluteExpiration
                ?? (options.AbsoluteExpirationRelativeToNow.HasValue
                    ? _timeProvider.GetUtcNow().Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : _timeProvider.GetUtcNow().AddMinutes(20));

            _entries[key] = new CacheRecord(value, absoluteExpiration);
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

        private byte[]? TryGetValue(string key)
        {
            if (!_entries.TryGetValue(key, out var value))
            {
                return null;
            }

            if (value.ExpiresAtUtc <= _timeProvider.GetUtcNow())
            {
                _entries.TryRemove(key, out _);
                return null;
            }

            return value.Payload.ToArray();
        }

        private sealed record CacheRecord(byte[] Payload, DateTimeOffset ExpiresAtUtc);
    }

    private sealed class RecordingEventPublisher : IDataImportExportEventPublisher
    {
        public List<DataImportExportEvent> Events { get; } = [];

        public ValueTask PublishAsync(DataImportExportEvent notification, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(notification);
            return ValueTask.CompletedTask;
        }
    }
}
