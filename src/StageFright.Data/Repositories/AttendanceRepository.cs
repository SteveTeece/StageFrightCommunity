using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class AttendanceRepository : BaseRepository<AttendanceRecord>, IAttendanceRepository
{
    public AttendanceRepository(StageFrightDbContext db) : base(db) { }

    public async Task<bool> ExistsAsync(Guid rehearsalId, Guid memberId, CancellationToken ct = default)
    {
        return await _db.AttendanceRecords
            .IgnoreQueryFilters()
            .AnyAsync(a => a.RehearsalId == rehearsalId && a.MemberId == memberId, ct);
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default)
    {
        try
        {
            return await _db.AttendanceRecords
                .Include(a => a.Member)
                .Where(a => a.RehearsalId == rehearsalId)
                .OrderBy(a => a.Member.Name)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(AttendanceRecord), nameof(GetByRehearsalAsync), null, ex);
        }
    }

    public async Task AddBatchAsync(IReadOnlyList<AttendanceRecord> records, CancellationToken ct = default)
    {
        try
        {
            await _db.AttendanceRecords.AddRangeAsync(records, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(AttendanceRecord), nameof(AddBatchAsync), null, ex);
        }
    }
}
