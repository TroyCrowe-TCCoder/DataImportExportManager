namespace DataImportExportManager.Interfaces;

using DataImportExportManager.Contracts;

/// <summary>
/// Publishes import/export lifecycle notifications for integration scenarios.
/// </summary>
public interface IDataImportExportEventPublisher
{
    /// <summary>
    /// Publishes a library event notification.
    /// </summary>
    /// <param name="notification">The event payload to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    ValueTask PublishAsync(DataImportExportEvent notification, CancellationToken cancellationToken = default);
}
