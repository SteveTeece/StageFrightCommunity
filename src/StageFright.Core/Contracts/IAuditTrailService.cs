using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service that writes audit trail entries.
/// UserId is always "system" in the MVP (NFR-013).
/// </summary>
public interface IAuditTrailService
{
    /// <summary>Records a state-change action for the given entity.</summary>
    Task LogAsync(string entityType, Guid entityId, AuditAction action,
        string? oldValue = null, string? newValue = null,
        string userId = "system", CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes audit trail entries older than the cutoff. Called at startup with a
    /// cutoff derived from the configured retention period. Failure is caught, logged,
    /// and does not propagate — startup continues regardless.
    /// </summary>
    Task PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
