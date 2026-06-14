using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class ParticipationRepository : SoftDeletableBaseRepository<ParticipationRecord>, IParticipationRepository
{
    public ParticipationRepository(StageFrightDbContext db) : base(db) { }

    public async Task<bool> ExistsAsync(Guid eventId, Guid memberId, CancellationToken ct = default)
    {
        return await _db.ParticipationRecords
            .IgnoreQueryFilters()
            .AnyAsync(p => p.EventId == eventId && p.MemberId == memberId, ct);
    }

    public async Task AddBatchAsync(IReadOnlyList<ParticipationRecord> records, CancellationToken ct = default)
    {
        try
        {
            await _db.ParticipationRecords.AddRangeAsync(records, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(ParticipationRecord), nameof(AddBatchAsync), null, ex);
        }
    }
}
