using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class AgmRepository : SoftDeletableBaseRepository<AnnualGeneralMeeting>, IAgmRepository
{
    public AgmRepository(StageFrightDbContext db) : base(db) { }

    public async Task<IReadOnlyList<AnnualGeneralMeeting>> GetPastOrderedAsync(CancellationToken ct = default)
    {
        return await _db.AnnualGeneralMeetings
            .OrderByDescending(a => a.Date)
            .ToListAsync(ct);
    }
}
