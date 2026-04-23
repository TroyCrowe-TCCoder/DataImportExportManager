namespace DataImportExportManager.Contracts;

/// <summary>
/// Represents a lifecycle notification emitted by the data import/export library.
/// </summary>
/// <param name="EventName">The event name identifying the lifecycle transition.</param>
/// <param name="OccurredAtUtc">The UTC timestamp for when the event occurred.</param>
/// <param name="Extension">The normalized extension related to the operation, when available.</param>
/// <param name="TenantId">The tenant scope identifier, when available.</param>
/// <param name="SubjectId">The subject/user identifier, when available.</param>
/// <param name="SessionId">The schema session identifier, when available.</param>
/// <param name="DecisionCode">The schema decision code, when available.</param>
/// <param name="AvailableActions">The available schema mismatch actions, when available.</param>
/// <param name="Message">Optional descriptive detail for the event.</param>
public sealed record DataImportExportEvent(
    string EventName,
    DateTimeOffset OccurredAtUtc,
    string? Extension = null,
    string? TenantId = null,
    string? SubjectId = null,
    string? SessionId = null,
    string? DecisionCode = null,
    IReadOnlyList<SchemaMismatchAction>? AvailableActions = null,
    string? Message = null);
