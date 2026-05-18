namespace DataImportExportManager.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using DataImportExportManager.Exporters;
using DataImportExportManager.Extensions;
using DataImportExportManager.Importers;
using DataImportExportManager.Interfaces;
using DataImportExportManager.Services;
using Microsoft.Extensions.DependencyInjection;

public class DiagnosticsConformanceTests
{
    private static readonly Regex DiagnosticPattern =
        new("^\\[DIXMGR:[^:\\]]+:[A-Z]+:[A-Z0-9_]+\\] .+", RegexOptions.Compiled);

    private static readonly Regex DiagnosticCodePattern =
        new("^\\[DIXMGR:[^:\\]]+:[A-Z]+:(?<code>[A-Z0-9_]+)\\] ", RegexOptions.Compiled);

    [Fact]
    public async Task WhenDirectHandlerValidationFailsThenDiagnosticMessageMatchesContractPattern()
    {
        var failures = new List<Exception>
        {
            Assert.Throws<ArgumentException>(() => new CsvImporter(new CsvImporterOptions { Delimiter = '"' })),
            Assert.Throws<ArgumentException>(() => new CsvExporter(new CsvExporterOptions { Delimiter = '\n' })),
            await Assert.ThrowsAsync<InvalidOperationException>(() => new JsonImporter().ImportAsync(ToStream("[{]")).AsTask()),
            await Assert.ThrowsAsync<InvalidOperationException>(() => new NdjsonImporter().ImportAsync(ToStream("{\"id\":1\n")).AsTask()),
            await Assert.ThrowsAsync<InvalidOperationException>(() => new XmlImporter().ImportAsync(ToStream("<rows><row>")).AsTask()),
            await Assert.ThrowsAsync<ArgumentException>(() => new ExcelExporter(new ExcelExporterOptions { SheetName = "" }).ExportAsync([["A"]], new MemoryStream()).AsTask()),
        };

        foreach (var ex in failures)
        {
            Assert.Matches(DiagnosticPattern, ex.Message);
            AssertDiagnosticCodeIsCataloged(ex.Message);
        }
    }

    [Fact]
    public void WhenCatalogOperationsEnumeratedThenAllAreKnown()
    {
        foreach (var operation in DiagnosticsCatalogTestHelper.GetAllOperations())
        {
            Assert.True(DiagnosticsCatalogTestHelper.IsKnownOperation(operation));
        }
    }

    [Fact]
    public void WhenCatalogCodesEnumeratedThenAllAreKnown()
    {
        foreach (var code in DiagnosticsCatalogTestHelper.GetAllCodes())
        {
            Assert.True(DiagnosticsCatalogTestHelper.IsKnownCode(code));
        }
    }

    [Fact]
    public void WhenUnknownOperationThenBuildMessageThrowsArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(
            () => DiagnosticsCatalogTestHelper.BuildMessage(".json", "INVALID_OP", DiagnosticsCatalogTestHelper.InvalidRootKindCode, "detail"));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void WhenUnknownCodeThenBuildMessageThrowsArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(
            () => DiagnosticsCatalogTestHelper.BuildMessage(".json", DiagnosticsCatalogTestHelper.ImportOperation, "UNKNOWN_CODE", "detail"));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void WhenKnownInputsThenBuildMessageMatchesContractPattern()
    {
        var message = DiagnosticsCatalogTestHelper.BuildMessage(
            ".json",
            DiagnosticsCatalogTestHelper.ImportOperation,
            DiagnosticsCatalogTestHelper.InvalidRootKindCode,
            "JSON import requires a root array.");

        Assert.Matches(DiagnosticPattern, message);
    }

    [Fact]
    public async Task WhenRouterFailureOccursThenDiagnosticMessageMatchesContractPattern()
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager(configureExcelExporter: options => options.SheetName = "");
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();

        var routeFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(".unknown", new MemoryStream()).AsTask());

        var contractFailure = await Assert.ThrowsAsync<ArgumentException>(
            () => router.ExportAsync(".xlsx", [["A"]], new MemoryStream()).AsTask());

        Assert.Matches(DiagnosticPattern, routeFailure.Message);
        Assert.Matches(DiagnosticPattern, contractFailure.Message);
        AssertDiagnosticCodeIsCataloged(routeFailure.Message);
        AssertDiagnosticCodeIsCataloged(contractFailure.Message);
    }

    [Fact]
    public void WhenDuplicateHandlerRegistrationFailsThenDiagnosticMessageMatchesContractPattern()
    {
        var duplicateImporterEx = Assert.Throws<InvalidOperationException>(
            () => new DataFormatRouter(
                [new StubImporter(".csv"), new StubImporter("csv")],
                [new StubExporter(".csv")]));

        var duplicateExporterEx = Assert.Throws<InvalidOperationException>(
            () => new DataFormatRouter(
                [new StubImporter(".csv")],
                [new StubExporter(".csv"), new StubExporter("CSV")]));

        Assert.Matches(DiagnosticPattern, duplicateImporterEx.Message);
        Assert.Matches(DiagnosticPattern, duplicateExporterEx.Message);
        AssertDiagnosticCodeIsCataloged(duplicateImporterEx.Message);
        AssertDiagnosticCodeIsCataloged(duplicateExporterEx.Message);
        Assert.Contains(":DUPLICATE_HANDLER]", duplicateImporterEx.Message, StringComparison.Ordinal);
        Assert.Contains(":DUPLICATE_HANDLER]", duplicateExporterEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WhenReadmeDiagnosticsTablePresentThenCodesStayInSyncWithCatalog()
    {
        var readmeCodes = GetReadmeDiagnosticsCodes();
        var catalogCodes = DiagnosticsCatalogTestHelper.GetAllCodes();

        Assert.NotEmpty(readmeCodes);

        foreach (var readmeCode in readmeCodes)
        {
            Assert.Contains(readmeCode, catalogCodes);
        }

        foreach (var requiredCode in catalogCodes)
        {
            Assert.Contains(requiredCode, readmeCodes);
        }
    }

    [Fact]
    public void WhenReadmeOperationsTablePresentThenOperationsStayInSyncWithCatalog()
    {
        var readmeOperations = GetReadmeDiagnosticsOperations();
        var catalogOperations = DiagnosticsCatalogTestHelper.GetAllOperations();

        Assert.NotEmpty(readmeOperations);

        foreach (var readmeOperation in readmeOperations)
        {
            Assert.Contains(readmeOperation, catalogOperations);
        }

        foreach (var requiredOperation in catalogOperations)
        {
            Assert.Contains(requiredOperation, readmeOperations);
        }
    }

    private static void AssertDiagnosticCodeIsCataloged(string message)
    {
        var match = DiagnosticCodePattern.Match(message);
        Assert.True(match.Success, "Diagnostic message did not include a parsable code segment.");

        var code = match.Groups["code"].Value;
        Assert.Contains(code, DiagnosticsCatalogTestHelper.GetAllCodes());
    }

    private static HashSet<string> GetReadmeDiagnosticsCodes([CallerFilePath] string sourceFile = "")
    {
        var testsProjectDir = Path.GetDirectoryName(sourceFile)!;
        var readmePath = Path.GetFullPath(Path.Combine(testsProjectDir, "..", "README.md"));

        Assert.True(File.Exists(readmePath), $"README.md not found at expected path: {readmePath}");

        var lines = File.ReadAllLines(readmePath);
        var tableHeaderIndex = Array.FindIndex(lines, line => line.Contains("### Diagnostics Code Reference (Current)", StringComparison.Ordinal));
        Assert.True(tableHeaderIndex >= 0, "Diagnostics code reference table header was not found in README.md.");

        var codeRegex = new Regex("`(?<code>[A-Z0-9_]+)`", RegexOptions.Compiled);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var tableStarted = false;

        for (var i = tableHeaderIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                if (tableStarted)
                {
                    break;
                }

                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            tableStarted = true;

            var match = codeRegex.Match(line);
            if (match.Success)
            {
                codes.Add(match.Groups["code"].Value);
            }
        }

        return codes;
    }

    private static HashSet<string> GetReadmeDiagnosticsOperations([CallerFilePath] string sourceFile = "")
    {
        var testsProjectDir = Path.GetDirectoryName(sourceFile)!;
        var readmePath = Path.GetFullPath(Path.Combine(testsProjectDir, "..", "README.md"));

        Assert.True(File.Exists(readmePath), $"README.md not found at expected path: {readmePath}");

        var lines = File.ReadAllLines(readmePath);
        var tableHeaderIndex = Array.FindIndex(lines, line => line.Contains("### Diagnostics Operation Reference (Current)", StringComparison.Ordinal));
        Assert.True(tableHeaderIndex >= 0, "Diagnostics operation reference table header was not found in README.md.");

        var operationRegex = new Regex("`(?<operation>[A-Z]+)`", RegexOptions.Compiled);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var tableStarted = false;

        for (var i = tableHeaderIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                if (tableStarted)
                {
                    break;
                }

                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            tableStarted = true;

            var match = operationRegex.Match(line);
            if (match.Success)
            {
                operations.Add(match.Groups["operation"].Value);
            }
        }

        return operations;
    }

    private static MemoryStream ToStream(string content)
        => new(Encoding.UTF8.GetBytes(content));

    private sealed class StubImporter(string ext) : IDataImporter
    {
        public string SupportedExtension { get; } = ext;

        public ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(Stream source, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<IReadOnlyList<string>>>([]);
    }

    private sealed class StubExporter(string ext) : IDataExporter
    {
        public string SupportedExtension { get; } = ext;

        public ValueTask ExportAsync(IReadOnlyList<IReadOnlyList<string>> data, Stream destination, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
