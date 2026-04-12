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
    public static string BuildImportMessage(string extension, string code, string detail)
        => BuildMessage(extension, Operations.Import, code, detail);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string BuildExportMessage(string extension, string code, string detail)
        => BuildMessage(extension, Operations.Export, code, detail);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string BuildConfigMessage(string extension, string code, string detail)
        => BuildMessage(extension, Operations.Config, code, detail);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string BuildMessage(string extension, string operation, string code, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        if (!IsKnownOperation(operation))
        {
            throw new ArgumentException($"Unknown diagnostics operation '{operation}'.", nameof(operation));
        }

        if (!IsKnownCode(code))
        {
            throw new ArgumentException($"Unknown diagnostics code '{code}'.", nameof(code));
        }

        return $"{Prefix}{extension}:{operation}:{code}] {detail}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasPrefix(string message)
        => message.StartsWith(Prefix, StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsKnownOperation(string operation)
        => operation is Operations.Import or Operations.Export or Operations.Config;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsKnownCode(string code)
        => code is
            Codes.RouteNotFound
            or Codes.Contract
            or Codes.DuplicateHandler
            or Codes.InvalidDelimiter
            or Codes.InvalidRootKind
            or Codes.InvalidRecordKind
            or Codes.MalformedJson
            or Codes.MixedRecordTypes
            or Codes.BlankLine
            or Codes.InvalidRoot
            or Codes.MalformedXml
            or Codes.InvalidRowElement
            or Codes.SchemaModeViolation
            or Codes.ObjectRowsDisabled
            or Codes.MixedRowSchemas
            or Codes.InvalidHeader
            or Codes.DuplicateHeader
            or Codes.RowWidthMismatch
            or Codes.InvalidWorkbook
            or Codes.SheetNotFound
            or Codes.BufferLimitExceeded
            or Codes.InvalidSheetIndex
            or Codes.InvalidSheetName
            or Codes.RowLimitExceeded;
}
