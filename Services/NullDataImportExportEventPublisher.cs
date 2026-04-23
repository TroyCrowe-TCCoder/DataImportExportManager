namespace DataImportExportManager.Services;

using DataImportExportManager.Contracts;
using DataImportExportManager.Interfaces;

internal sealed class NullDataImportExportEventPublisher : IDataImportExportEventPublisher
{
    public ValueTask PublishAsync(DataImportExportEvent notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
