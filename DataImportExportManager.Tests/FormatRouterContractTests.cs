namespace DataImportExportManager.Tests;

using System.Text;
using DataImportExportManager.Extensions;
using DataImportExportManager.Interfaces;
using Microsoft.Extensions.DependencyInjection;

public class FormatRouterContractTests
{
    [Theory]
    [InlineData(".csv")]
    [InlineData(".tsv")]
    [InlineData(".json")]
    [InlineData(".ndjson")]
    [InlineData("jsonl")]
    [InlineData(".xml")]
    [InlineData(".xlsx")]
    public async Task WhenExportThenImportViaRouterThenRoundTripMatchesRows(string extension)
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();
        IReadOnlyList<IReadOnlyList<string>> data =
        [
            ["Name", "Code"],
            ["O'Neil", "ACME-01"],
        ];

        using var destination = new MemoryStream();
        await router.ExportAsync(extension, data, destination);
        destination.Position = 0;

        var roundTrip = await router.ImportAsync(extension, destination);

        Assert.Equal(data.Count, roundTrip.Count);
        for (var i = 0; i < data.Count; i++)
        {
            Assert.Equal(data[i], roundTrip[i]);
        }
    }

    [Theory]
    [InlineData(".jsonl")]
    [InlineData("jsonl")]
    [InlineData(".NDJSON")]
    public void WhenNdjsonAliasProvidedThenRouterResolvesNdjsonHandlers(string extension)
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();

        Assert.Equal(".ndjson", router.GetImporter(extension).SupportedExtension);
        Assert.Equal(".ndjson", router.GetExporter(extension).SupportedExtension);
    }

    [Theory]
    [InlineData(".json", "{}", nameof(DiagnosticsCatalogTestHelper.InvalidRootKindCode))]
    [InlineData(".ndjson", "123\n", nameof(DiagnosticsCatalogTestHelper.InvalidRecordKindCode))]
    [InlineData(".xml", "<root><row><cell>A</cell></row></root>", nameof(DiagnosticsCatalogTestHelper.InvalidRootCode))]
    public async Task WhenImportContractViolationThenRouterPreservesInternalDiagnosticCode(
        string extension,
        string payload,
        string expectedCodeProperty)
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var expectedCode = ResolveCode(expectedCodeProperty);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(extension, source).AsTask());

        Assert.StartsWith($"[DIXMGR:{extension.ToLowerInvariant()}:{DiagnosticsCatalogTestHelper.ImportOperation}:", ex.Message, StringComparison.Ordinal);
        Assert.Contains($":{expectedCode}]", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain($":{DiagnosticsCatalogTestHelper.ContractCode}]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenImportRouteMissingThenRouterThrowsRouteDiagnosticCode()
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(".unknown", new MemoryStream()).AsTask());

        Assert.StartsWith($"[DIXMGR:.unknown:{DiagnosticsCatalogTestHelper.ImportOperation}:{DiagnosticsCatalogTestHelper.RouteNotFoundCode}]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenExportRouteMissingThenRouterThrowsRouteDiagnosticCode()
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();
        IReadOnlyList<IReadOnlyList<string>> data = [["A"]];

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ExportAsync(".unknown", data, new MemoryStream()).AsTask());

        Assert.StartsWith($"[DIXMGR:.unknown:{DiagnosticsCatalogTestHelper.ExportOperation}:{DiagnosticsCatalogTestHelper.RouteNotFoundCode}]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenExcelImportContractViolationThenRouterPreservesExcelInternalDiagnosticCode()
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager(configureExcelImporter: options => options.SheetName = "DoesNotExist");
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();
        var exporter = provider.GetServices<IDataExporter>().Single(e => e.SupportedExtension == ".xlsx");

        IReadOnlyList<IReadOnlyList<string>> data = [["A"]];
        using var stream = new MemoryStream();
        await exporter.ExportAsync(data, stream);
        stream.Position = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(".xlsx", stream).AsTask());

        Assert.Contains($":{DiagnosticsCatalogTestHelper.SheetNotFoundCode}]", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain($":{DiagnosticsCatalogTestHelper.ContractCode}]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenExcelExportContractViolationThenRouterPreservesExcelInternalDiagnosticCode()
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager(configureExcelExporter: options => options.SheetName = string.Empty);
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();
        IReadOnlyList<IReadOnlyList<string>> data = [["A"]];

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => router.ExportAsync(".xlsx", data, new MemoryStream()).AsTask());

        Assert.Contains($":{DiagnosticsCatalogTestHelper.InvalidSheetNameCode}]", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".json", "[{]", nameof(DiagnosticsCatalogTestHelper.MalformedJsonCode))]
    [InlineData(".ndjson", "{\"id\":1\n", nameof(DiagnosticsCatalogTestHelper.MalformedJsonCode))]
    [InlineData(".xml", "<rows><row><cell>A</cell>", nameof(DiagnosticsCatalogTestHelper.MalformedXmlCode))]
    public async Task WhenMalformedPayloadThenRouterPreservesInternalDiagnosticCode(
        string extension,
        string payload,
        string expectedCodeProperty)
    {
        var services = new ServiceCollection();
        services.AddDataImportExportManager();
        using var provider = services.BuildServiceProvider();

        var router = provider.GetRequiredService<IDataFormatRouter>();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var expectedCode = ResolveCode(expectedCodeProperty);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(extension, source).AsTask());

        Assert.Contains($":{expectedCode}]", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain($":{DiagnosticsCatalogTestHelper.ContractCode}]", ex.Message, StringComparison.Ordinal);
    }

    private static string ResolveCode(string codePropertyName)
        => codePropertyName switch
        {
            nameof(DiagnosticsCatalogTestHelper.InvalidRootKindCode) => DiagnosticsCatalogTestHelper.InvalidRootKindCode,
            nameof(DiagnosticsCatalogTestHelper.InvalidRecordKindCode) => DiagnosticsCatalogTestHelper.InvalidRecordKindCode,
            nameof(DiagnosticsCatalogTestHelper.InvalidRootCode) => DiagnosticsCatalogTestHelper.InvalidRootCode,
            nameof(DiagnosticsCatalogTestHelper.MalformedJsonCode) => DiagnosticsCatalogTestHelper.MalformedJsonCode,
            nameof(DiagnosticsCatalogTestHelper.MalformedXmlCode) => DiagnosticsCatalogTestHelper.MalformedXmlCode,
            _ => throw new InvalidOperationException($"Unknown diagnostics code property '{codePropertyName}'."),
        };
}
