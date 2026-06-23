using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class EventRepository : SoftDeletableBaseRepository<Event>, IEventRepository
{
    public EventRepository(StageFrightDbContext db) : base(db) { }

    public async Task<Event?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1);
        return await _db.Events
            .Where(e => e.Date < endOfDay)
            .OrderByDescending(e => e.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Event?> GetMostRecentPastWithoutParticipationAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1);
        return await _db.Events
            .Where(e => e.Date < endOfDay && e.StoredParticipationRate == null)
            .OrderByDescending(e => e.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Event?> GetMostRecentPastWithParticipationAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1);
        return await _db.Events
            .Where(e => e.Date < endOfDay && e.StoredParticipationRate != null)
            .OrderByDescending(e => e.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Event?> GetNextUpcomingAsync(DateTime asOf, CancellationToken ct = default)
    {
        var startOfDay = asOf.Date;
        return await _db.Events
            .Where(e => e.Date >= startOfDay)
            .OrderBy(e => e.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> AgmExistsInYearAsync(int year, CancellationToken ct = default)
    {
        return await _db.Events
            .Include(e => e.EventType)
            .Where(e => e.Date.Year == year
                && e.EventType.Name == "Annual General Meeting")
            .AnyAsync(ct);
    }
}
