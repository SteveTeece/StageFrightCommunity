using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class CommitteePositionRecordRepository : SoftDeletableBaseRepository<CommitteePositionRecord>, ICommitteePositionRecordRepository
{
    public CommitteePositionRecordRepository(StageFrightDbContext db) : base(db) { }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        return await _db.CommitteePositionRecords
            .Include(c => c.CommitteeTerm)
            .Include(c => c.OfficeHolderType)
            .Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.Year)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByYearAsync(int year, CancellationToken ct = default)
    {
        return await _db.CommitteePositionRecords
            .Where(c => c.Year == year)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByTermAsync(Guid committeeTermId, CancellationToken ct = default)
    {
        return await _db.CommitteePositionRecords
            .Where(c => c.CommitteeTermId == committeeTermId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByAgmAsync(Guid annualGeneralMeetingId, CancellationToken ct = default)
    {
        return await _db.CommitteePositionRecords
            .Where(c => c.CommitteeTerm != null && c.CommitteeTerm.StartedByAgmId == annualGeneralMeetingId)
            .ToListAsync(ct);
    }

    public async Task<CommitteePositionRecord?> GetOpenByMemberInTermAsync(Guid committeeTermId, Guid memberId, CancellationToken ct = default)
    {
        return await _db.CommitteePositionRecords
            .FirstOrDefaultAsync(c => c.CommitteeTermId == committeeTermId && c.MemberId == memberId && c.EndDate == null, ct);
    }
}
