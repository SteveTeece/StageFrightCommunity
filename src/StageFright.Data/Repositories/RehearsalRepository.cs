using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class RehearsalRepository : SoftDeletableBaseRepository<Rehearsal>, IRehearsalRepository
{
    public RehearsalRepository(StageFrightDbContext db) : base(db) { }

    public async Task<Rehearsal?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1);
        return await _db.Rehearsals
            .Where(r => r.Date < endOfDay)
            .OrderByDescending(r => r.Date)
            .FirstOrDefaultAsync(ct);
    }
}
