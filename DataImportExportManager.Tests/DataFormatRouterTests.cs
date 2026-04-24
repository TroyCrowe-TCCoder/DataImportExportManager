namespace DataImportExportManager.Tests;

using DataImportExportManager.Contracts;
using DataImportExportManager.Interfaces;
using DataImportExportManager.Services;

public class DataFormatRouterTests
{
    [Fact]
    public void WhenExtensionWithoutDotThenReturnsMatchingImporter()
    {
        var importer = new TestImporter(".csv");
        var router = CreateRouter([importer], [new TestExporter(".csv")]);

        var selected = router.GetImporter("csv");

        Assert.Same(importer, selected);
    }

    [Fact]
    public void WhenExtensionUsesDifferentCaseThenReturnsMatchingExporter()
    {
        var exporter = new TestExporter(".xlsx");
        var router = CreateRouter([new TestImporter(".xlsx")], [exporter]);

        var selected = router.GetExporter(".XLSX");

        Assert.Same(exporter, selected);
    }

    [Fact]
    public void WhenDuplicateImporterExtensionThenThrowsInvalidOperationException()
    {
        var first = new TestImporter(".csv");
        var second = new TestImporter("CSV");

        var ex = Assert.Throws<InvalidOperationException>(
            () => CreateRouter([first, second], [new TestExporter(".csv")]));

        Assert.Contains("Multiple importers", ex.Message);
    }

    [Fact]
    public void WhenDuplicateExporterExtensionThenThrowsInvalidOperationException()
    {
        var first = new TestExporter(".xlsx");
        var second = new TestExporter("xlsx");

        var ex = Assert.Throws<InvalidOperationException>(
            () => CreateRouter([new TestImporter(".xlsx")], [first, second]));

        Assert.Contains("Multiple exporters", ex.Message);
    }

    [Fact]
    public async Task WhenImportAsyncThenDelegatesToSelectedImporter()
    {
        var importer = new TestImporter(".csv")
        {
            Result = [["A", "B"], ["1", "2"]],
        };
        var router = CreateRouter([importer], [new TestExporter(".csv")]);
        using var source = new MemoryStream([1, 2, 3]);

        var result = await router.ImportAsync("CSV", source);

        Assert.Equal(1, importer.CallCount);
        Assert.Same(source, importer.LastSource);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task WhenImportAsyncThenPublishesStartedAndCompletedEvents()
    {
        var publisher = new RecordingEventPublisher();
        var importer = new TestImporter(".csv")
        {
            Result = [["A"], ["1"]],
        };

        var router = CreateRouter([importer], [new TestExporter(".csv")], publisher);
        using var source = new MemoryStream([1]);

        _ = await router.ImportAsync(".csv", source);

        Assert.Collection(
            publisher.Events,
            first =>
            {
                Assert.Equal(DataImportExportEventNames.ImportStarted, first.EventName);
                Assert.Equal(".csv", first.Extension);
                Assert.Equal("Import operation started.", first.Message);
                Assert.True(first.OccurredAtUtc <= DateTimeOffset.UtcNow);
            },
            second =>
            {
                Assert.Equal(DataImportExportEventNames.ImportCompleted, second.EventName);
                Assert.Equal(".csv", second.Extension);
                Assert.Equal("Import operation completed with 2 rows.", second.Message);
                Assert.True(second.OccurredAtUtc <= DateTimeOffset.UtcNow);
            });
    }

    [Fact]
    public async Task WhenExportAsyncThenDelegatesToSelectedExporter()
    {
        var exporter = new TestExporter(".xlsx");
        var router = CreateRouter([new TestImporter(".xlsx")], [exporter]);

        IReadOnlyList<IReadOnlyList<string>> data = [["H1"], ["V1"]];
        using var destination = new MemoryStream();

        await router.ExportAsync("xlsx", data, destination);

        Assert.Equal(1, exporter.CallCount);
        Assert.Same(data, exporter.LastData);
        Assert.Same(destination, exporter.LastDestination);
    }

    [Fact]
    public async Task WhenExportAsyncThenPublishesStartedAndCompletedEvents()
    {
        var publisher = new RecordingEventPublisher();
        var exporter = new TestExporter(".xlsx");
        var router = CreateRouter([new TestImporter(".xlsx")], [exporter], publisher);

        IReadOnlyList<IReadOnlyList<string>> data = [["H1"], ["V1"]];
        using var destination = new MemoryStream();

        await router.ExportAsync("xlsx", data, destination);

        Assert.Collection(
            publisher.Events,
            first =>
            {
                Assert.Equal(DataImportExportEventNames.ExportStarted, first.EventName);
                Assert.Equal(".xlsx", first.Extension);
                Assert.Equal("Export operation started.", first.Message);
                Assert.True(first.OccurredAtUtc <= DateTimeOffset.UtcNow);
            },
            second =>
            {
                Assert.Equal(DataImportExportEventNames.ExportCompleted, second.EventName);
                Assert.Equal(".xlsx", second.Extension);
                Assert.Equal("Export operation completed with 2 rows.", second.Message);
                Assert.True(second.OccurredAtUtc <= DateTimeOffset.UtcNow);
            });
    }

    [Fact]
    public async Task WhenImporterMissingThenImportAsyncThrowsInvalidOperationException()
    {
        var router = CreateRouter([], []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(".json", new MemoryStream()).AsTask());
    }

    [Fact]
    public async Task WhenImporterThrowsUnprefixedInvalidOperationThenRouterWrapsWithContractDiagnostic()
    {
        var importer = new TestImporter(".json")
        {
            ImportException = new InvalidOperationException("Importer contract failed."),
        };
        var router = CreateRouter([importer], [new TestExporter(".json")]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(".json", new MemoryStream()).AsTask());

        Assert.StartsWith("[DIXMGR:.json:IMPORT:CONTRACT]", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
        Assert.Equal("Importer contract failed.", ex.InnerException!.Message);
    }

    [Fact]
    public async Task WhenImporterThrowsThenPublishesFailedEvent()
    {
        var publisher = new RecordingEventPublisher();
        var importer = new TestImporter(".json")
        {
            ImportException = new InvalidOperationException("Importer contract failed."),
        };

        var router = CreateRouter([importer], [new TestExporter(".json")], publisher);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(".json", new MemoryStream()).AsTask());

        Assert.Equal(2, publisher.Events.Count);
        Assert.Equal(DataImportExportEventNames.ImportStarted, publisher.Events[0].EventName);
        Assert.Equal(DataImportExportEventNames.ImportFailed, publisher.Events[1].EventName);
        Assert.Equal(".json", publisher.Events[1].Extension);
        Assert.StartsWith("[DIXMGR:.json:IMPORT:CONTRACT]", publisher.Events[1].Message, StringComparison.Ordinal);
        Assert.True(publisher.Events[1].OccurredAtUtc <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".ndjson")]
    public async Task WhenImporterThrowsUnprefixedArgumentExceptionThenRouterWrapsWithConfigDiagnostic(string extension)
    {
        var importer = new TestImporter(extension)
        {
            ImportException = new ArgumentException("Importer options are invalid.", nameof(extension)),
        };
        var router = CreateRouter([importer], [new TestExporter(extension)]);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => router.ImportAsync(extension, new MemoryStream()).AsTask());

        Assert.StartsWith($"[DIXMGR:{extension}:CONFIG:CONTRACT]", ex.Message, StringComparison.Ordinal);
        Assert.Equal(nameof(extension), ex.ParamName);
        Assert.NotNull(ex.InnerException);
        Assert.Contains("Importer options are invalid.", ex.InnerException!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".ndjson")]
    public async Task WhenExporterThrowsUnprefixedArgumentExceptionThenRouterWrapsWithConfigDiagnostic(string extension)
    {
        var exporter = new TestExporter(extension)
        {
            ExportException = new ArgumentException("Exporter options are invalid.", nameof(extension)),
        };
        var router = CreateRouter([new TestImporter(extension)], [exporter]);
        IReadOnlyList<IReadOnlyList<string>> data = [["A"]];

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => router.ExportAsync(extension, data, new MemoryStream()).AsTask());

        Assert.StartsWith($"[DIXMGR:{extension}:CONFIG:CONTRACT]", ex.Message, StringComparison.Ordinal);
        Assert.Equal(nameof(extension), ex.ParamName);
        Assert.NotNull(ex.InnerException);
        Assert.Contains("Exporter options are invalid.", ex.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenHandlerThrowsPrefixedDiagnosticThenRouterPassesThroughWithoutDoubleWrap()
    {
        const string prefixed = "[DIXMGR:.json:IMPORT:INVALID_ROOT_KIND] JSON import requires root array.";
        var importer = new TestImporter(".json")
        {
            ImportException = new InvalidOperationException(prefixed),
        };
        var router = CreateRouter([importer], [new TestExporter(".json")]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ImportAsync(".json", new MemoryStream()).AsTask());

        Assert.Equal(prefixed, ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task WhenHandlerThrowsPrefixedArgumentDiagnosticThenRouterPassesThroughWithoutDoubleWrap()
    {
        const string prefixed = "[DIXMGR:.json:CONFIG:INVALID_SHEET_NAME] SheetName cannot be empty.";
        var exporter = new TestExporter(".json")
        {
            ExportException = new ArgumentException(prefixed),
        };
        var router = CreateRouter([new TestImporter(".json")], [exporter]);
        IReadOnlyList<IReadOnlyList<string>> data = [["A"]];

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => router.ExportAsync(".json", data, new MemoryStream()).AsTask());

        Assert.Equal(prefixed, ex.Message);
        Assert.Null(ex.ParamName);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void WhenJsonlExtensionThenResolvesNdjsonImporter()
    {
        var importer = new TestImporter(".ndjson");
        var router = CreateRouter([importer], [new TestExporter(".ndjson")]);

        var selected = router.GetImporter(".jsonl");

        Assert.Same(importer, selected);
    }

    [Fact]
    public void WhenJsonlExtensionThenResolvesNdjsonExporter()
    {
        var exporter = new TestExporter(".ndjson");
        var router = CreateRouter([new TestImporter(".ndjson")], [exporter]);

        var selected = router.GetExporter("jsonl");

        Assert.Same(exporter, selected);
    }

    private static DataFormatRouter CreateRouter(
        IEnumerable<IDataImporter> importers,
        IEnumerable<IDataExporter> exporters,
        IDataImportExportEventPublisher? publisher = null)
        => new(importers, exporters, publisher);

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

    private sealed class TestImporter(string supportedExtension) : IDataImporter
    {
        public string SupportedExtension { get; } = supportedExtension;

        public int CallCount { get; private set; }

        public Stream? LastSource { get; private set; }

        public IReadOnlyList<IReadOnlyList<string>> Result { get; init; } = [];

        public Exception? ImportException { get; init; }

        public ValueTask<IReadOnlyList<IReadOnlyList<string>>> ImportAsync(Stream source, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSource = source;
            if (ImportException is not null)
            {
                throw ImportException;
            }

            return ValueTask.FromResult(Result);
        }
    }

    private sealed class TestExporter(string supportedExtension) : IDataExporter
    {
        public string SupportedExtension { get; } = supportedExtension;

        public int CallCount { get; private set; }

        public IReadOnlyList<IReadOnlyList<string>>? LastData { get; private set; }

        public Stream? LastDestination { get; private set; }

        public Exception? ExportException { get; init; }

        public ValueTask ExportAsync(IReadOnlyList<IReadOnlyList<string>> data, Stream destination, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastData = data;
            LastDestination = destination;

            if (ExportException is not null)
            {
                throw ExportException;
            }

            return ValueTask.CompletedTask;
        }
    }
}
