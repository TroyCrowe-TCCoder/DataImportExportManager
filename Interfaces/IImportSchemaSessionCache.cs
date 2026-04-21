namespace DataImportExportManager.Interfaces;

using DataImportExportManager.Contracts;

/// <summary>
/// Provides temporary tenant-scoped caching for imported schema payloads.
/// </summary>
public interface IImportSchemaSessionCache
{
    /// <summary>
    /// Stores an imported payload in a temporary session.
    /// </summary>
    /// <param name="tenantId">The tenant scope identifier.</param>
    /// <param name="subjectId">The subject/user identifier within the tenant scope.</param>
    /// <param name="importResult">The imported payload to cache.</param>
    /// <param name="timeToLive">Optional per-session TTL; when omitted, implementation default is used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created session identifier.</returns>
    ValueTask<string> StoreAsync(
        string tenantId,
        string subjectId,
        TabularImportResult importResult,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a cached session payload when still valid.
    /// </summary>
    /// <param name="tenantId">The tenant scope identifier.</param>
    /// <param name="subjectId">The subject/user identifier within the tenant scope.</param>
    /// <param name="sessionId">The session identifier returned by <see cref="StoreAsync"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached session payload, or <see langword="null"/> when not found or expired.</returns>
    ValueTask<ImportSchemaSession?> TryGetAsync(
        string tenantId,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached session payload.
    /// </summary>
    /// <param name="tenantId">The tenant scope identifier.</param>
    /// <param name="subjectId">The subject/user identifier within the tenant scope.</param>
    /// <param name="sessionId">The session identifier to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a session was removed; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> RemoveAsync(
        string tenantId,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken = default);
}
