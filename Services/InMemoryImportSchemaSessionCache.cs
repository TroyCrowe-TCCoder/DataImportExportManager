namespace DataImportExportManager.Services;

using System.Collections.Concurrent;
using DataImportExportManager.Contracts;
using DataImportExportManager.Interfaces;

/// <summary>
/// In-memory implementation of <see cref="IImportSchemaSessionCache"/> for short-lived import sessions.
/// </summary>
public sealed class InMemoryImportSchemaSessionCache : IImportSchemaSessionCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _defaultTtl;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryImportSchemaSessionCache"/> class.
    /// </summary>
    /// <param name="defaultTtl">Default session TTL when not provided per call.</param>
    /// <param name="timeProvider">Optional time provider for deterministic testing.</param>
    public InMemoryImportSchemaSessionCache(TimeSpan? defaultTtl = null, TimeProvider? timeProvider = null)
    {
        var effectiveDefaultTtl = defaultTtl ?? TimeSpan.FromMinutes(20);
        if (effectiveDefaultTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTtl), "Default TTL must be greater than zero.");
        }

        _defaultTtl = effectiveDefaultTtl;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public ValueTask<string> StoreAsync(
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
        _sessions[key] = new CacheEntry(importResult, expiresAtUtc);

        return ValueTask.FromResult(sessionId);
    }

    /// <inheritdoc/>
    public ValueTask<ImportSchemaSession?> TryGetAsync(
        string tenantId,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScopeInputs(tenantId, subjectId);
        ValidateSessionId(sessionId);

        var key = BuildKey(tenantId, subjectId, sessionId);
        if (!_sessions.TryGetValue(key, out var entry))
        {
            return ValueTask.FromResult<ImportSchemaSession?>(null);
        }

        var now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAtUtc <= now)
        {
            _sessions.TryRemove(key, out _);
            return ValueTask.FromResult<ImportSchemaSession?>(null);
        }

        var session = new ImportSchemaSession(sessionId, tenantId.Trim(), subjectId.Trim(), entry.ImportResult, entry.ExpiresAtUtc);
        return ValueTask.FromResult<ImportSchemaSession?>(session);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RemoveAsync(
        string tenantId,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScopeInputs(tenantId, subjectId);
        ValidateSessionId(sessionId);

        var key = BuildKey(tenantId, subjectId, sessionId);
        var removed = _sessions.TryRemove(key, out _);
        return ValueTask.FromResult(removed);
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

    private static string BuildKey(string tenantId, string subjectId, string sessionId)
        => $"{tenantId.Trim()}::{subjectId.Trim()}::{sessionId.Trim()}";

    private sealed record CacheEntry(TabularImportResult ImportResult, DateTimeOffset ExpiresAtUtc);
}
