using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.AuditTrail;

/// <summary>
/// Writes audit trail entries through IAuditTrailRepository.
/// Performs the startup purge (entries older than 12 months). Purge failure is tolerated —
/// a structured error is logged but startup continues (FR-022).
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
    /// Hard-deletes audit trail entries older than 12 months. Called at startup.
    /// Failure is caught, logged, and startup continues.
    /// </summary>
    public async Task PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        try
        {
            var count = await _repository.PurgeOlderThanAsync(cutoff, ct);
            _logger.LogInformation("Audit trail purge complete: {Count} entries removed", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit trail purge failed; startup continues");
        }
    }
}
