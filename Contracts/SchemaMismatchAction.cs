namespace DataImportExportManager.Contracts;

/// <summary>
/// Represents available actions when imported schema does not match expected schema.
/// </summary>
public enum SchemaMismatchAction
{
    /// <summary>
    /// Indicates no action is required.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates the source file should be corrected to match expected schema.
    /// </summary>
    CorrectSourceFile = 1,

    /// <summary>
    /// Indicates processing can continue after forcing a schema remap.
    /// </summary>
    ContinueWithRemap = 2,
}
