namespace DataImportExportManager.Tests;

using DataImportExportManager.Contracts;
using DataImportExportManager.Interfaces;
using DataImportExportManager.Services;

public class InMemoryImportSchemaSessionCacheTests
{
    [Fact]
    public async Task WhenStoredThenTryGetReturnsSessionWithoutReupload()
    {
        var cache = new InMemoryImportSchemaSessionCache();
        var importResult = new TabularImportResult(["Id"], [["Id"], ["1"]]);

        var sessionId = await cache.StoreAsync("tenant-a", "user-a", importResult);
        var session = await cache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.NotNull(session);
        Assert.Equal(sessionId, session!.SessionId);
        Assert.Equal("tenant-a", session.TenantId);
        Assert.Equal("user-a", session.SubjectId);
        Assert.Equal(importResult, session.ImportResult);
    }

    [Fact]
    public async Task WhenStoredThenPublishesSessionStoredEvent()
    {
        var publisher = new RecordingEventPublisher();
        var cache = new InMemoryImportSchemaSessionCache(eventPublisher: publisher);

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
        var cache = new InMemoryImportSchemaSessionCache();
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        var tenantMismatch = await cache.TryGetAsync("tenant-b", "user-a", sessionId);
        var subjectMismatch = await cache.TryGetAsync("tenant-a", "user-b", sessionId);

        Assert.Null(tenantMismatch);
        Assert.Null(subjectMismatch);
    }

    [Fact]
    public async Task WhenExpiredThenSessionIsNotReturned()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var cache = new InMemoryImportSchemaSessionCache(defaultTtl: TimeSpan.FromMinutes(5), timeProvider: clock);

        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));
        clock.Advance(TimeSpan.FromMinutes(6));

        var session = await cache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.Null(session);
    }

    [Fact]
    public async Task WhenRemovedThenSessionCannotBeRetrieved()
    {
        var cache = new InMemoryImportSchemaSessionCache();
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        var removed = await cache.RemoveAsync("tenant-a", "user-a", sessionId);
        var session = await cache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.True(removed);
        Assert.Null(session);
    }

    [Fact]
    public async Task WhenConsumedThenSessionReturnsOnceAndIsInvalidated()
    {
        var cache = new InMemoryImportSchemaSessionCache();
        var importResult = new TabularImportResult(["Id"], [["Id"], ["1"]]);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", importResult);

        var consumed = await cache.ConsumeAsync("tenant-a", "user-a", sessionId);
        var afterConsume = await cache.TryGetAsync("tenant-a", "user-a", sessionId);

        Assert.NotNull(consumed);
        Assert.Equal(importResult, consumed!.ImportResult);
        Assert.Null(afterConsume);
    }

    [Fact]
    public async Task WhenConsumedThenPublishesSessionConsumedEvent()
    {
        var publisher = new RecordingEventPublisher();
        var cache = new InMemoryImportSchemaSessionCache(eventPublisher: publisher);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        _ = await cache.ConsumeAsync("tenant-a", "user-a", sessionId);

        var consumedEvent = Assert.Single(publisher.Events.Where(e => e.EventName == DataImportExportEventNames.SessionConsumed));
        Assert.Equal("tenant-a", consumedEvent.TenantId);
        Assert.Equal("user-a", consumedEvent.SubjectId);
        Assert.Equal(sessionId, consumedEvent.SessionId);
        Assert.Equal("Schema session consumed.", consumedEvent.Message);
    }

    [Fact]
    public async Task WhenConsumedAfterExpirationThenReturnsNull()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var cache = new InMemoryImportSchemaSessionCache(defaultTtl: TimeSpan.FromMinutes(5), timeProvider: clock);
        var sessionId = await cache.StoreAsync("tenant-a", "user-a", new TabularImportResult(["Id"], [["Id"]]));

        clock.Advance(TimeSpan.FromMinutes(6));
        var consumed = await cache.ConsumeAsync("tenant-a", "user-a", sessionId);

        Assert.Null(consumed);
    }

    [Fact]
    public async Task WhenScopeValuesInvalidThenStoreThrowsArgumentException()
    {
        var cache = new InMemoryImportSchemaSessionCache();

        await Assert.ThrowsAsync<ArgumentException>(() => cache.StoreAsync("", "user-a", new TabularImportResult(["Id"], [["Id"]])).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => cache.StoreAsync("tenant-a", "", new TabularImportResult(["Id"], [["Id"]])).AsTask());
    }

    [Fact]
    public async Task WhenCancellationRequestedThenOperationsThrowOperationCanceledException()
    {
        var cache = new InMemoryImportSchemaSessionCache();
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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
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
