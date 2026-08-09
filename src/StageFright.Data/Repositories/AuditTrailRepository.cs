using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class AuditTrailRepository : IAuditTrailRepository
{
    private readonly StageFrightDbContext _db;

    public AuditTrailRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AuditTrailEntry entry, CancellationToken ct = default)
    {
        try
        {
            await _db.AuditTrailEntries.AddAsync(entry, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(AuditTrailEntry), nameof(AddAsync), null, ex);
        }
    }

    public async Task<IReadOnlyList<AuditTrailEntry>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        return await _db.AuditTrailEntries
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<int> PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        try
        {
            var old = await _db.AuditTrailEntries
                .Where(a => a.Timestamp < cutoff)
                .ToListAsync(ct);

            _db.AuditTrailEntries.RemoveRange(old);
            await _db.SaveChangesAsync(ct);
            return old.Count;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(AuditTrailEntry), nameof(PurgeOlderThanAsync), null, ex);
        }
    }
}
