namespace DataImportExportManager.Services;

/// <summary>
/// Options for <see cref="DistributedImportSchemaSessionCache"/> behavior.
/// </summary>
public sealed class DistributedImportSchemaSessionCacheOptions
{
    /// <summary>
    /// Gets or sets the default cache key prefix used for session entries.
    /// </summary>
    public string KeyPrefix { get; set; } = "dixmgr-sess";

    /// <summary>
    /// Gets or sets the maximum serialized payload size in bytes allowed for a session entry.
    /// </summary>
    public int MaxPayloadBytes { get; set; } = 1_048_576;
}
