namespace DataImportExportManager.Contracts;

/// <summary>
/// Well-known event names emitted by import/export workflows.
/// </summary>
public static class DataImportExportEventNames
{
    /// <summary>
    /// Indicates a format-routed import operation has started.
    /// </summary>
    public const string ImportStarted = "import.started";

    /// <summary>
    /// Indicates a format-routed import operation has completed.
    /// </summary>
    public const string ImportCompleted = "import.completed";

    /// <summary>
    /// Indicates a format-routed import operation has failed.
    /// </summary>
    public const string ImportFailed = "import.failed";

    /// <summary>
    /// Indicates a format-routed export operation has started.
    /// </summary>
    public const string ExportStarted = "export.started";

    /// <summary>
    /// Indicates a format-routed export operation has completed.
    /// </summary>
    public const string ExportCompleted = "export.completed";

    /// <summary>
    /// Indicates a format-routed export operation has failed.
    /// </summary>
    public const string ExportFailed = "export.failed";

    /// <summary>
    /// Indicates schema validation has detected a mismatch requiring action.
    /// </summary>
    public const string SchemaValidationMismatch = "schema.validation.mismatch";

    /// <summary>
    /// Indicates a schema session was stored.
    /// </summary>
    public const string SessionStored = "session.stored";

    /// <summary>
    /// Indicates a schema session was consumed.
    /// </summary>
    public const string SessionConsumed = "session.consumed";

    /// <summary>
    /// Indicates a schema session was removed.
    /// </summary>
    public const string SessionRemoved = "session.removed";
}
