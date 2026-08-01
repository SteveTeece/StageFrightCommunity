using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class AgmAttendanceRepository : BaseRepository<AgmAttendanceRecord>, IAgmAttendanceRepository
{
    public AgmAttendanceRepository(StageFrightDbContext db) : base(db) { }

    public async Task AddRangeAsync(IEnumerable<AgmAttendanceRecord> records, CancellationToken ct = default)
    {
        try
        {
            await _db.AgmAttendanceRecords.AddRangeAsync(records, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(AgmAttendanceRecord), nameof(AddRangeAsync), null, ex);
        }
    }

    public async Task<IReadOnlyList<AgmAttendanceRecord>> GetByAgmAsync(Guid agmId, CancellationToken ct = default)
    {
        try
        {
            return await _db.AgmAttendanceRecords
                .Include(a => a.Member)
                .Where(a => a.AnnualGeneralMeetingId == agmId)
                .OrderBy(a => a.Member.LastName).ThenBy(a => a.Member.FirstName)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(AgmAttendanceRecord), nameof(GetByAgmAsync), null, ex);
        }
    }
}
