namespace DataImportExportManager.Contracts;

/// <summary>
/// Represents the outcome of validating imported headers against expected schema columns.
/// </summary>
public sealed record SchemaValidationResult(
    bool IsMatch,
    bool RequiresRemap,
    bool ShouldDeleteExistingMapping,
    IReadOnlyList<string> MissingColumns,
    IReadOnlyList<string> ExtraColumns,
    IReadOnlyList<string> DuplicateIncomingColumns,
    IReadOnlyList<SchemaMismatchAction> AvailableActions,
    string DecisionCode)
{
    /// <summary>
    /// Decision code used when imported schema matches expected schema.
    /// </summary>
    public const string SchemaMatchCode = "SCHEMA_MATCH";

    /// <summary>
    /// Decision code used when imported schema does not match expected schema.
    /// </summary>
    public const string SchemaMismatchCode = "SCHEMA_MISMATCH_REMAP_REQUIRED";
}
