namespace DataImportExportManager.Contracts;

/// <summary>
/// Represents a temporary cached import payload for schema decision workflows.
/// </summary>
public sealed record ImportSchemaSession(
    string SessionId,
    string TenantId,
    string SubjectId,
    TabularImportResult ImportResult,
    DateTimeOffset ExpiresAtUtc);
