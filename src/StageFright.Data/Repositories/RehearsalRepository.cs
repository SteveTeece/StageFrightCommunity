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

    public async Task<Rehearsal?> GetMostRecentPastWithoutAttendanceAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1);
        return await _db.Rehearsals
            .Where(r => r.Date < endOfDay && r.StoredAttendanceRate == null)
            .OrderByDescending(r => r.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Rehearsal?> GetMostRecentPastWithAttendanceAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1);
        return await _db.Rehearsals
            .Where(r => r.Date < endOfDay && r.StoredAttendanceRate != null)
            .OrderByDescending(r => r.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Rehearsal?> GetNextUpcomingAsync(DateTime asOf, CancellationToken ct = default)
    {
        var startOfDay = asOf.Date;
        return await _db.Rehearsals
            .Where(r => r.Date >= startOfDay)
            .OrderBy(r => r.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Rehearsal>> GetYearToDateWithAttendanceAsync(int year, CancellationToken ct = default)
    {
        return await _db.Rehearsals
            .Where(r => r.Date.Year == year && r.StoredAttendanceRate != null)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);
    }
}
