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
}
