using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.AuditTrail;

/// <summary>
/// Writes audit trail entries through IAuditTrailRepository.
/// Performs the startup purge of entries past the configured retention period. A purge failure is
/// NOT swallowed here — it propagates to the startup sequence, which logs it and surfaces it as a
/// non-fatal warning so it is never silently discarded (spec 028, US8 / FR-025); startup still
/// continues.
/// LogAsync no-ops while an AuditTrailSuppressionScope is active on the current async flow
/// (used by the debug data seeder to avoid writing thousands of synthetic audit entries).
/// </summary>
public class AuditTrailService : IAuditTrailService
{
    private readonly IAuditTrailRepository _repository;
    private readonly ILogger<AuditTrailService> _logger;

    public AuditTrailService(IAuditTrailRepository repository, ILogger<AuditTrailService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task LogAsync(string entityType, Guid entityId, AuditAction action,
        string? oldValue = null, string? newValue = null,
        string userId = "system", CancellationToken ct = default)
    {
        if (AuditTrailSuppressionScope.IsSuppressed)
            return;

        var entry = new AuditTrailEntry
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        };

        await _repository.AddAsync(entry, ct);
    }

    /// <summary>
    /// Hard-deletes audit trail entries older than the given cutoff. Called at startup with a
    /// cutoff derived from the configured retention period (Settings.AuditRetentionYears).
    /// A failure propagates to the caller (the startup sequence), which logs it and surfaces it as
    /// a non-fatal startup warning — it is never silently discarded (spec 028, FR-025). Startup
    /// still continues.
    /// </summary>
    public async Task PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        var count = await _repository.PurgeOlderThanAsync(cutoff, ct);
        _logger.LogInformation("Audit trail purge complete: {Count} entries removed", count);
    }
}
