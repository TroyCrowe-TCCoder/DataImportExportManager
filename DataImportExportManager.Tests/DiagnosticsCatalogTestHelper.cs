namespace DataImportExportManager.Tests;

using System.Reflection;
using DataImportExportManager.Services;

internal static class DiagnosticsCatalogTestHelper
{
    public static string ImportOperation => GetOperation(nameof(ImportOperation));
    public static string ExportOperation => GetOperation(nameof(ExportOperation));
    public static string ConfigOperation => GetOperation(nameof(ConfigOperation));

    public static string RouteNotFoundCode => GetCode(nameof(RouteNotFoundCode));
    public static string ContractCode => GetCode(nameof(ContractCode));
    public static string DuplicateHandlerCode => GetCode(nameof(DuplicateHandlerCode));
    public static string InvalidSheetNameCode => GetCode(nameof(InvalidSheetNameCode));
    public static string InvalidRootKindCode => GetCode(nameof(InvalidRootKindCode));
    public static string InvalidRecordKindCode => GetCode(nameof(InvalidRecordKindCode));
    public static string InvalidRootCode => GetCode(nameof(InvalidRootCode));
    public static string MalformedJsonCode => GetCode(nameof(MalformedJsonCode));
    public static string MalformedXmlCode => GetCode(nameof(MalformedXmlCode));
    public static string SheetNotFoundCode => GetCode(nameof(SheetNotFoundCode));

    public static HashSet<string> GetAllOperations()
        => GetNestedType("Operations")
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    public static HashSet<string> GetAllCodes()
        => GetNestedType("Codes")
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    public static bool IsKnownOperation(string operation)
        => (bool)(GetContractDiagnosticsType()
            .GetMethod("IsKnownOperation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
            .Invoke(null, [operation])
            ?? throw new InvalidOperationException("ContractDiagnostics.IsKnownOperation was not found."));

    public static bool IsKnownCode(string code)
        => (bool)(GetContractDiagnosticsType()
            .GetMethod("IsKnownCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
            .Invoke(null, [code])
            ?? throw new InvalidOperationException("ContractDiagnostics.IsKnownCode was not found."));

    public static string BuildMessage(string extension, string operation, string code, string detail)
        => (string)(GetContractDiagnosticsType()
            .GetMethod("BuildMessage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
            .Invoke(null, [extension, operation, code, detail])
            ?? throw new InvalidOperationException("ContractDiagnostics.BuildMessage was not found."));

    private static string GetOperation(string propertyName)
        => propertyName switch
        {
            nameof(ImportOperation) => GetOperationsField("Import"),
            nameof(ExportOperation) => GetOperationsField("Export"),
            nameof(ConfigOperation) => GetOperationsField("Config"),
            _ => throw new InvalidOperationException($"Unknown operation property '{propertyName}'."),
        };

    private static string GetCode(string propertyName)
        => propertyName switch
        {
            nameof(RouteNotFoundCode) => GetCodesField("RouteNotFound"),
            nameof(ContractCode) => GetCodesField("Contract"),
            nameof(DuplicateHandlerCode) => GetCodesField("DuplicateHandler"),
            nameof(InvalidSheetNameCode) => GetCodesField("InvalidSheetName"),
            nameof(InvalidRootKindCode) => GetCodesField("InvalidRootKind"),
            nameof(InvalidRecordKindCode) => GetCodesField("InvalidRecordKind"),
            nameof(InvalidRootCode) => GetCodesField("InvalidRoot"),
            nameof(MalformedJsonCode) => GetCodesField("MalformedJson"),
            nameof(MalformedXmlCode) => GetCodesField("MalformedXml"),
            nameof(SheetNotFoundCode) => GetCodesField("SheetNotFound"),
            _ => throw new InvalidOperationException($"Unknown code property '{propertyName}'."),
        };

    private static string GetOperationsField(string fieldName)
        => GetNestedType("Operations")
            .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
            .GetRawConstantValue() as string
            ?? throw new InvalidOperationException($"Diagnostics operation field '{fieldName}' not found.");

    private static string GetCodesField(string fieldName)
        => GetNestedType("Codes")
            .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
            .GetRawConstantValue() as string
            ?? throw new InvalidOperationException($"Diagnostics code field '{fieldName}' not found.");

    private static Type GetNestedType(string nestedTypeName)
        => GetContractDiagnosticsType()
            .GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Diagnostics nested type '{nestedTypeName}' not found.");

    private static Type GetContractDiagnosticsType()
        => typeof(DataFormatRouter)
            .Assembly
            .GetType("DataImportExportManager.Diagnostics.ContractDiagnostics")
            ?? throw new InvalidOperationException("ContractDiagnostics type not found.");
}
