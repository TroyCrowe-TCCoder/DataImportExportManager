namespace DataImportExportManager.Diagnostics;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides deterministic, machine-readable diagnostics for contract and routing failures.
/// </summary>
internal static class ContractDiagnostics
{
    private const string Prefix = "[DIXMGR:";

    internal static class Operations
    {
        public const string Import = "IMPORT";
        public const string Export = "EXPORT";
        public const string Config = "CONFIG";
    }

    internal static class Codes
    {
        public const string RouteNotFound = "ROUTE_NOT_FOUND";
        public const string Contract = "CONTRACT";
        public const string DuplicateHandler = "DUPLICATE_HANDLER";

        public const string InvalidDelimiter = "INVALID_DELIMITER";

        public const string InvalidRootKind = "INVALID_ROOT_KIND";
        public const string InvalidRecordKind = "INVALID_RECORD_KIND";
        public const string MalformedJson = "MALFORMED_JSON";
        public const string MixedRecordTypes = "MIXED_RECORD_TYPES";
        public const string BlankLine = "BLANK_LINE";

        public const string InvalidRoot = "INVALID_ROOT";
        public const string MalformedXml = "MALFORMED_XML";
        public const string InvalidRowElement = "INVALID_ROW_ELEMENT";
        public const string SchemaModeViolation = "SCHEMA_MODE_VIOLATION";
        public const string ObjectRowsDisabled = "OBJECT_ROWS_DISABLED";
        public const string MixedRowSchemas = "MIXED_ROW_SCHEMAS";
        public const string InvalidHeader = "INVALID_HEADER";
        public const string DuplicateHeader = "DUPLICATE_HEADER";
        public const string RowWidthMismatch = "ROW_WIDTH_MISMATCH";

        public const string InvalidWorkbook = "INVALID_WORKBOOK";
        public const string SheetNotFound = "SHEET_NOT_FOUND";
        public const string BufferLimitExceeded = "BUFFER_LIMIT_EXCEEDED";
        public const string InvalidSheetIndex = "INVALID_SHEET_INDEX";
        public const string InvalidSheetName = "INVALID_SHEET_NAME";
        public const string RowLimitExceeded = "ROW_LIMIT_EXCEEDED";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string BuildMessage(string extension, string operation, string code, string detail)
        => $"{Prefix}{extension}:{operation}:{code}] {detail}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasPrefix(string message)
        => message.StartsWith(Prefix, StringComparison.Ordinal);
}
