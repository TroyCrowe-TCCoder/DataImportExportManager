namespace DataImportExportManager.Services;

using System.Text.Json;
using DataImportExportManager.Contracts;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// Distributed-cache implementation of <see cref="IImportSchemaSessionCache"/> for multi-instance hosting.
/// </summary>
public sealed class DistributedImportSchemaSessionCache : IImportSchemaSessionCache
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
    };

    private readonly IDistributedCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _defaultTtl;
    private readonly string _keyPrefix;
    private readonly int _maxPayloadBytes;
    private readonly IDataImportExportEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedImportSchemaSessionCache"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache backend.</param>
    /// <param name="defaultTtl">Default session TTL when not provided per call.</param>
    /// <param name="options">Optional cache options for key prefix and payload-size limits.</param>
    /// <param name="timeProvider">Optional time provider for deterministic testing.</param>
    /// <param name="eventPublisher">Optional event publisher for lifecycle notifications.</param>
    public DistributedImportSchemaSessionCache(
        IDistributedCache cache,
        TimeSpan? defaultTtl = null,
        DistributedImportSchemaSessionCacheOptions? options = null,
        TimeProvider? timeProvider = null,
        IDataImportExportEventPublisher? eventPublisher = null)
    {
        ArgumentNullException.ThrowIfNull(cache);

        var effectiveDefaultTtl = defaultTtl ?? TimeSpan.FromMinutes(20);
        if (effectiveDefaultTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTtl), "Default TTL must be greater than zero.");
        }

        _cache = cache;
        _defaultTtl = effectiveDefaultTtl;
        _timeProvider = timeProvider ?? TimeProvider.System;

        var effectiveOptions = options ?? new DistributedImportSchemaSessionCacheOptions();
        if (string.IsNullOrWhiteSpace(effectiveOptions.KeyPrefix))
        {
            throw new ArgumentException("KeyPrefix cannot be null, empty, or whitespace.", nameof(options));
        }

        if (effectiveOptions.MaxPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxPayloadBytes must be greater than zero.");
        }

        _keyPrefix = effectiveOptions.KeyPrefix.Trim();
        _maxPayloadBytes = effectiveOptions.MaxPayloadBytes;
        _eventPublisher = eventPublisher ?? new NullDataImportExportEventPublisher();
    }

    /// <inheritdoc/>
    public async ValueTask<string> StoreAsync(
        string tenantId,
        string subjectId,
        TabularImportResult importResult,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScopeInputs(tenantId, subjectId);
        ArgumentNullException.ThrowIfNull(importResult);

        var ttl = timeToLive ?? _defaultTtl;
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "TTL must be greater than zero.");
        }

        var now = _timeProvider.GetUtcNow();
        var sessionId = Guid.NewGuid().ToString("N");
        var expiresAtUtc = now.Add(ttl);
        var key = BuildKey(tenantId, subjectId, sessionId);

        var entry = new CacheEntry(importResult, expiresAtUtc);
        byte[] payload;
        try
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(entry, _jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Session payload serialization failed.", ex);
        }

        if (payload.Length > _maxPayloadBytes)
        {
            throw new InvalidOperationException($"Session payload exceeds configured maximum size of {_maxPayloadBytes} bytes.");
        }

        await _cache.SetAsync(
            key,
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAtUtc },
            cancellationToken).ConfigureAwait(false);

        await _eventPublisher.PublishAsync(
            new DataImportExportEvent(
                EventName: DataImportExportEventNames.SessionStored,
                OccurredAtUtc: now,
                TenantId: tenantId.Trim(),
                SubjectId: subjectId.Trim(),
                SessionId: sessionId,
                Message: "Schema session stored."),
            cancellationToken).ConfigureAwait(false);

        return sessionId;
    }

    /// <inheritdoc/>
    public async ValueTask<ImportSchemaSession?> TryGetAsync(
        string tenantId,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScopeInputs(tenantId, subjectId);
        ValidateSessionId(sessionId);

        var key = BuildKey(tenantId, subjectId, sessionId);
        var payload = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return null;
        }

        var entry = DeserializeEntry(payload);
        var now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAtUtc <= now)
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            await _eventPublisher.PublishAsync(
                new DataImportExportEvent(
                    EventName: DataImportExportEventNames.SessionRemoved,
                    OccurredAtUtc: now,
                    TenantId: tenantId.Trim(),
                    SubjectId: subjectId.Trim(),
                    SessionId: sessionId.Trim(),
                    Message: "Schema session expired and was removed."),
                cancellationToken).ConfigureAwait(false);

            return null;
        }

        return CreateSession(tenantId, subjectId, sessionId, entry);
    }

    /// <inheritdoc/>
    public async ValueTask<ImportSchemaSession?> ConsumeAsync(
        string tenantId,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScopeInputs(tenantId, subjectId);
        ValidateSessionId(sessionId);

        var key = BuildKey(tenantId, subjectId, sessionId);
        var payload = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return null;
        }

        await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);

        var entry = DeserializeEntry(payload);
        var now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAtUtc <= now)
        {
            await _eventPublisher.PublishAsync(
                new DataImportExportEvent(
                    EventName: DataImportExportEventNames.SessionRemoved,
                    OccurredAtUtc: now,
                    TenantId: tenantId.Trim(),
                    SubjectId: subjectId.Trim(),
                    SessionId: sessionId.Trim(),
                    Message: "Schema session expired before consume."),
                cancellationToken).ConfigureAwait(false);

            return null;
        }

        await _eventPublisher.PublishAsync(
            new DataImportExportEvent(
                EventName: DataImportExportEventNames.SessionConsumed,
                OccurredAtUtc: now,
                TenantId: tenantId.Trim(),
                SubjectId: subjectId.Trim(),
                SessionId: sessionId.Trim(),
                Message: "Schema session consumed."),
            cancellationToken).ConfigureAwait(false);

        return CreateSession(tenantId, subjectId, sessionId, entry);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(
        string tenantId,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScopeInputs(tenantId, subjectId);
        ValidateSessionId(sessionId);

        var key = BuildKey(tenantId, subjectId, sessionId);
        var payload = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return false;
        }

        await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);

        await _eventPublisher.PublishAsync(
            new DataImportExportEvent(
                EventName: DataImportExportEventNames.SessionRemoved,
                OccurredAtUtc: _timeProvider.GetUtcNow(),
                TenantId: tenantId.Trim(),
                SubjectId: subjectId.Trim(),
                SessionId: sessionId.Trim(),
                Message: "Schema session removed."),
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static void ValidateScopeInputs(string tenantId, string subjectId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("SubjectId cannot be null, empty, or whitespace.", nameof(subjectId));
        }
    }

    private static void ValidateSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("SessionId cannot be null, empty, or whitespace.", nameof(sessionId));
        }
    }

    private static CacheEntry DeserializeEntry(byte[] payload)
    {
        var entry = JsonSerializer.Deserialize<CacheEntry>(payload, _jsonOptions);
        if (entry is null)
        {
            throw new InvalidOperationException("Cached session payload could not be deserialized.");
        }

        return entry;
    }

    private string BuildKey(string tenantId, string subjectId, string sessionId)
        => $"{_keyPrefix}::{tenantId.Trim()}::{subjectId.Trim()}::{sessionId.Trim()}";

    private static ImportSchemaSession CreateSession(string tenantId, string subjectId, string sessionId, CacheEntry entry)
        => new(sessionId, tenantId.Trim(), subjectId.Trim(), entry.ImportResult, entry.ExpiresAtUtc);

    private sealed record CacheEntry(TabularImportResult ImportResult, DateTimeOffset ExpiresAtUtc);
}
