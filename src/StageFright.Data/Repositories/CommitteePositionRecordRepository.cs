using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class CommitteePositionRecordRepository : SoftDeletableBaseRepository<CommitteePositionRecord>, ICommitteePositionRecordRepository
{
    public CommitteePositionRecordRepository(StageFrightDbContext db) : base(db) { }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        var records = await _db.CommitteePositionRecords
            .Include(c => c.CommitteeTerm)
            .Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.Year)
            .ToListAsync(ct);

        await AttachOfficeHolderTypesAsync(records, ct);
        return records;
    }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByYearAsync(int year, CancellationToken ct = default)
    {
        return await _db.CommitteePositionRecords
            .Where(c => c.Year == year)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByTermAsync(Guid committeeTermId, CancellationToken ct = default)
    {
        var records = await _db.CommitteePositionRecords
            .Include(c => c.Member)
            .Where(c => c.CommitteeTermId == committeeTermId)
            .ToListAsync(ct);

        await AttachOfficeHolderTypesAsync(records, ct);
        return records;
    }

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetByAgmAsync(Guid annualGeneralMeetingId, CancellationToken ct = default)
    {
        var records = await _db.CommitteePositionRecords
            .Include(c => c.Member)
            .Where(c => c.CommitteeTerm != null && c.CommitteeTerm.StartedByAgmId == annualGeneralMeetingId)
            .ToListAsync(ct);

        await AttachOfficeHolderTypesAsync(records, ct);
        return records;
    }

    public async Task<CommitteePositionRecord?> GetOpenByMemberInTermAsync(Guid committeeTermId, Guid memberId, CancellationToken ct = default)
    {
        return await _db.CommitteePositionRecords
            .FirstOrDefaultAsync(c => c.CommitteeTermId == committeeTermId && c.MemberId == memberId && c.EndDate == null, ct);
    }

    /// <summary>
    /// Loads and attaches OfficeHolderType by id, bypassing the entity's own soft-delete query
    /// filter (a plain .Include would silently null the navigation once the type is archived —
    /// historical AGM/committee records must keep showing an archived title's name).
    /// </summary>
    private async Task AttachOfficeHolderTypesAsync(IReadOnlyList<CommitteePositionRecord> records, CancellationToken ct)
    {
        var typeIds = records
            .Where(r => r.OfficeHolderTypeId.HasValue)
            .Select(r => r.OfficeHolderTypeId!.Value)
            .Distinct()
            .ToList();
        if (typeIds.Count == 0) return;

        var types = await _db.CommitteeOfficeHolderTypes
            .IgnoreQueryFilters()
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        foreach (var record in records)
        {
            if (record.OfficeHolderTypeId.HasValue && types.TryGetValue(record.OfficeHolderTypeId.Value, out var type))
                record.OfficeHolderType = type;
        }
    }
}
