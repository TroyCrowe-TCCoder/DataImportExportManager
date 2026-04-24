namespace DataImportExportManager.Tests;

using DataImportExportManager.Contracts;
using DataImportExportManager.Extensions;
using DataImportExportManager.Interfaces;
using DataImportExportManager.Interfaces;

public class DataImportSchemaExtensionsTests
{
    [Fact]
    public async Task WhenImporterRowsContainHeaderThenImportWithSchemaReturnsColumnsAndRows()
    {
        var importer = new TestImporter
        {
            Rows =
            [
                ["Id", "Name"],
                ["1", "Ada"],
                ["2", "Lin"],
            ],
        };
        using var source = new MemoryStream([1]);

        var result = await importer.ImportWithSchemaAsync(source);

        Assert.Equal(["Id", "Name"], result.Columns);
        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(["Id", "Name"], result.Rows[0]);
        Assert.Equal(2, result.DataRows.Count);
        Assert.Equal(["1", "Ada"], result.DataRows[0]);
    }

    [Fact]
    public async Task WhenImporterReturnsEmptyThenImportWithSchemaReturnsEmptyResult()
    {
        var importer = new TestImporter { Rows = [] };
        using var source = new MemoryStream([1]);

        var result = await importer.ImportWithSchemaAsync(source);

        Assert.Empty(result.Columns);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task WhenImporterReturnsHeaderOnlyThenImportWithSchemaReturnsColumnsWithoutRows()
    {
        var importer = new TestImporter { Rows = [["Id", "Name"]] };
        using var source = new MemoryStream([1]);

        var result = await importer.ImportWithSchemaAsync(source);

        Assert.Equal(["Id", "Name"], result.Columns);
        Assert.Single(result.Rows);
        Assert.Equal(["Id", "Name"], result.Rows[0]);
        Assert.Empty(result.DataRows);
    }

    [Fact]
    public async Task WhenRouterExtensionUsedThenImportWithSchemaDelegatesAndExtractsColumns()
    {
        using var source = new MemoryStream([1, 2]);
        var router = new TestRouter
        {
            ImportedRows =
            [
                ["ColA", "ColB"],
                ["ValueA", "ValueB"],
            ],
        };

        var result = await router.ImportWithSchemaAsync("csv", source);

        Assert.Equal("csv", router.LastExtension);
        Assert.Same(source, router.LastSource);
        Assert.Equal(["ColA", "ColB"], result.Columns);
        Assert.Equal(2, result.Rows.Count);
        Assert.Single(result.DataRows);
        Assert.Equal(["ValueA", "ValueB"], result.DataRows[0]);
    }

    [Fact]
    public async Task WhenImporterTupleHelperUsedThenReturnsHeaderAndDataRows()
    {
        var importer = new TestImporter
        {
            Rows =
            [
                ["Id", "Name"],
                ["1", "Ada"],
            ],
        };
        using var source = new MemoryStream([1]);

        var (headers, dataRows) = await importer.ImportWithSchemaTupleAsync(source);

        Assert.Equal(["Id", "Name"], headers);
        Assert.Single(dataRows);
        Assert.Equal(["1", "Ada"], dataRows[0]);
    }

    [Fact]
    public async Task WhenRouterTupleHelperUsedThenReturnsHeaderAndDataRows()
    {
        var router = new TestRouter
        {
            ImportedRows =
            [
                ["ColA", "ColB"],
                ["A", "B"],
            ],
        };
        using var source = new MemoryStream([1]);

        var (headers, dataRows) = await router.ImportWithSchemaTupleAsync(".csv", source);

        Assert.Equal(["ColA", "ColB"], headers);
        Assert.Single(dataRows);
        Assert.Equal(["A", "B"], dataRows[0]);
    }

    [Fact]
    public async Task WhenImporterBundleHelperUsedThenReturnsResultAndTupleProjection()
    {
        var importer = new TestImporter
        {
            Rows =
            [
                ["Id", "Name"],
                ["1", "Ada"],
            ],
        };
        using var source = new MemoryStream([1]);

        var (result, headers, dataRows) = await importer.ImportWithSchemaBundleAsync(source);

        Assert.Equal(["Id", "Name"], result.Columns);
        Assert.Equal(["Id", "Name"], headers);
        Assert.Single(dataRows);
        Assert.Equal(["1", "Ada"], dataRows[0]);
    }

    [Fact]
    public async Task WhenRouterBundleHelperUsedThenReturnsResultAndTupleProjection()
    {
        var router = new TestRouter
        {
            ImportedRows =
            [
                ["ColA", "ColB"],
                ["A", "B"],
            ],
        };
        using var source = new MemoryStream([1]);

        var (result, headers, dataRows) = await router.ImportWithSchemaBundleAsync(".csv", source);

        Assert.Equal(".csv", router.LastExtension);
        Assert.Equal(["ColA", "ColB"], result.Columns);
        Assert.Equal(["ColA", "ColB"], headers);
        Assert.Single(dataRows);
        Assert.Equal(["A", "B"], dataRows[0]);
    }

    [Fact]
    public async Task WhenCreateExampleImportFileWithDefaultSampleThenExportsHeaderAndGeneratedSample()
    {
        var router = new TestRouter();
        using var destination = new MemoryStream();

        await router.CreateExampleImportFileAsync(".csv", ["CustomerId", "CustomerName"], destination);

        Assert.Equal(".csv", router.LastExportExtension);
        Assert.Same(destination, router.LastDestination);
        Assert.NotNull(router.LastData);
        Assert.Equal(2, router.LastData!.Count);
        Assert.Equal(["CustomerId", "CustomerName"], router.LastData[0]);
        Assert.Equal(["sample_CustomerId", "sample_CustomerName"], router.LastData[1]);
    }

    [Fact]
    public async Task WhenCreateExampleImportFileWithCustomSampleThenExportsProvidedSample()
    {
        var router = new TestRouter();
        using var destination = new MemoryStream();

        await router.CreateExampleImportFileAsync(".json", ["Id", "Name"], destination, ["1", "Ada"]);

        Assert.NotNull(router.LastData);
        Assert.Equal(["Id", "Name"], router.LastData![0]);
        Assert.Equal(["1", "Ada"], router.LastData[1]);
    }

    [Fact]
    public async Task WhenCreateExampleImportFileAndHeadersEmptyThenThrowsArgumentException()
    {
        var router = new TestRouter();
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () => router.CreateExampleImportFileAsync(".csv", [], destination).AsTask());
    }

    [Fact]
    public async Task WhenCreateExampleImportFileAndSampleLengthMismatchedThenThrowsArgumentException()
    {
        var router = new TestRouter();
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () => router.CreateExampleImportFileAsync(".csv", ["Id", "Name"], destination, ["1"]).AsTask());
    }

    [Fact]
    public void WhenHeadersMatchExpectedSchemaThenValidationReturnsMatch()
    {
        var importResult = new TabularImportResult(
            ["Id", "Name"],
            [["Id", "Name"], ["1", "Ada"]]);

        var result = importResult.ValidateSchema(["Id", "Name"]);

        Assert.True(result.IsMatch);
        Assert.False(result.RequiresRemap);
        Assert.False(result.ShouldDeleteExistingMapping);
        Assert.Empty(result.MissingColumns);
        Assert.Empty(result.ExtraColumns);
        Assert.Empty(result.DuplicateIncomingColumns);
        Assert.Equal([SchemaMismatchAction.None], result.AvailableActions);
        Assert.Equal(SchemaValidationResult.SchemaMatchCode, result.DecisionCode);
    }

    [Fact]
    public void WhenHeadersDifferOnlyByCaseAndWhitespaceThenValidationStillMatches()
    {
        var importResult = new TabularImportResult(
            [" id ", "NAME"],
            [[" id ", "NAME"], ["1", "Ada"]]);

        var result = importResult.ValidateSchema(["Id", "Name"]);

        Assert.True(result.IsMatch);
        Assert.False(result.RequiresRemap);
    }

    [Fact]
    public void WhenHeadersMissingOrExtraThenValidationRequiresRemapAndActions()
    {
        var importResult = new TabularImportResult(
            ["Id", "LegacyField"],
            [["Id", "LegacyField"], ["1", "X"]]);

        var result = importResult.ValidateSchema(["Id", "Name"]);

        Assert.False(result.IsMatch);
        Assert.True(result.RequiresRemap);
        Assert.True(result.ShouldDeleteExistingMapping);
        Assert.Equal(["Name"], result.MissingColumns);
        Assert.Equal(["LegacyField"], result.ExtraColumns);
        Assert.Equal(
            [SchemaMismatchAction.CorrectSourceFile, SchemaMismatchAction.ContinueWithRemap],
            result.AvailableActions);
        Assert.Equal(SchemaValidationResult.SchemaMismatchCode, result.DecisionCode);
    }

    [Fact]
    public void WhenHeadersContainDuplicateThenValidationRequiresRemap()
    {
        var result = DataImportSchemaExtensions.ValidateSchema(
            importedHeaders: ["Id", "Name", "name"],
            expectedColumns: ["Id", "Name"]);

        Assert.False(result.IsMatch);
        Assert.True(result.RequiresRemap);
        Assert.Equal(["name"], result.DuplicateIncomingColumns);
    }

    [Fact]
    public async Task WhenValidateSchemaAsyncMismatchThenPublishesSchemaMismatchEvent()
    {
        var publisher = new RecordingEventPublisher();
        var importResult = new TabularImportResult(
            ["Id", "LegacyField"],
            [["Id", "LegacyField"], ["1", "X"]]);

        var result = await importResult.ValidateSchemaAsync(["Id", "Name"], publisher);

        Assert.False(result.IsMatch);
        Assert.Single(publisher.Events);
        Assert.Equal(DataImportExportEventNames.SchemaValidationMismatch, publisher.Events[0].EventName);
        Assert.Equal(SchemaValidationResult.SchemaMismatchCode, publisher.Events[0].DecisionCode);
        Assert.NotNull(publisher.Events[0].AvailableActions);
        Assert.Equal(2, publisher.Events[0].AvailableActions!.Count);
        Assert.Contains(SchemaMismatchAction.CorrectSourceFile, publisher.Events[0].AvailableActions);
        Assert.Contains(SchemaMismatchAction.ContinueWithRemap, publisher.Events[0].AvailableActions);
        Assert.Equal("Schema validation mismatch detected.", publisher.Events[0].Message);
        Assert.True(publisher.Events[0].OccurredAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task WhenValidateSchemaAsyncMatchThenDoesNotPublishSchemaMismatchEvent()
    {
        var publisher = new RecordingEventPublisher();
        var importResult = new TabularImportResult(
            ["Id", "Name"],
            [["Id", "Name"], ["1", "Ada"]]);

        var result = await importResult.ValidateSchemaAsync(["Id", "Name"], publisher);

        Assert.True(result.IsMatch);
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public void WhenExpectedColumnsContainDuplicateThenValidateSchemaThrowsArgumentException()
    {
        var importResult = new TabularImportResult(
            ["Id", "Name"],
            [["Id", "Name"]]);

        Assert.Throws<ArgumentException>(
            () => importResult.ValidateSchema(["Id", "id"]));
    }

    [Fact]
    public void WhenImportResultIsNullThenValidateSchemaThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => DataImportSchemaExtensions.ValidateSchema((TabularImportResult)null!, ["Id"]));
    }

    [Fact]
    public void WhenExpectedColumnsIsNullThenValidateSchemaThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => DataImportSchemaExtensions.ValidateSchema(importedHeaders: ["Id"], expectedColumns: null!));
    }

    [Fact]
    public async Task WhenImporterIsNullThenImportWithSchemaThrowsArgumentNullException()
    {
        using var source = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => DataImportSchemaExtensions.ImportWithSchemaAsync((IDataImporter)null!, source).AsTask());
    }

    [Fact]
    public async Task WhenRouterIsNullThenImportWithSchemaThrowsArgumentNullException()
    {
        using var source = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => DataImportSchemaExtensions.ImportWithSchemaAsync((IDataFormatRouter)null!, "csv", source).AsTask());
    }

    private sealed class TestImporter : IDataImporter
    {
        public string SupportedExtension => ".csv";

        public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];

        public ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            return ValueTask.FromResult(Rows);
        }
    }

    private sealed class TestRouter : IDataFormatRouter
    {
        public string? LastExtension { get; private set; }

        public string? LastExportExtension { get; private set; }

        public Stream? LastSource { get; private set; }

        public Stream? LastDestination { get; private set; }

        public IReadOnlyList<IReadOnlyList<string>> ImportedRows { get; init; } = [];

        public IReadOnlyList<IReadOnlyList<string>>? LastData { get; private set; }

        public IDataImporter GetImporter(string extension) => throw new NotSupportedException();

        public IDataExporter GetExporter(string extension) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(
            string extension,
            Stream source,
            CancellationToken cancellationToken = default)
        {
            LastExtension = extension;
            LastSource = source;
            return ValueTask.FromResult(ImportedRows);
        }

        public ValueTask ExportAsync(
            string extension,
            IReadOnlyList<IReadOnlyList<string>> data,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            LastExportExtension = extension;
            LastDestination = destination;
            LastData = data;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingEventPublisher : IDataImportExportEventPublisher
    {
        public List<DataImportExportEvent> Events { get; } = [];

        public ValueTask PublishAsync(DataImportExportEvent notification, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(notification);
            return ValueTask.CompletedTask;
        }
    }
}
