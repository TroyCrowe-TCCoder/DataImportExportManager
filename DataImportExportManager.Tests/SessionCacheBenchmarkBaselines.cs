namespace DataImportExportManager.Tests;

internal static class SessionCacheBenchmarkBaselines
{
    public const int IterationCount = 1_000;

    public const int InMemoryStoreGetConsumeMaxMilliseconds = 5_000;

    public const int DistributedStoreGetConsumeMaxMilliseconds = 8_000;
}
