using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for audit trail entries.
/// PurgeOlderThanAsync performs hard deletes (log-record exemption per Constitution §3.4).
/// </summary>
public interface IAuditTrailRepository
{
    /// <summary>Persists a new audit trail entry.</summary>
    Task AddAsync(AuditTrailEntry entry, CancellationToken ct = default);

    /// <summary>Returns all audit entries for the specified entity type and primary key.</summary>
    Task<IReadOnlyList<AuditTrailEntry>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>Hard-deletes all audit entries older than the cutoff timestamp. Returns the count deleted.</summary>
    Task<int> PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
