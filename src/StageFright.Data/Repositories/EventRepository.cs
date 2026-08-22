using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class EventRepository : SoftDeletableBaseRepository<Event>, IEventRepository
{
    public EventRepository(StageFrightDbContext db) : base(db) { }

    /// <summary>
    /// Overrides BaseRepository.GetAllAsync to enforce the date-descending order that
    /// IEventRepository/IEventService document — previously only applied client-side by callers.
    /// </summary>
    public override async Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Events
                .OrderByDescending(e => e.Date)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(Event), nameof(GetAllAsync), null, ex);
        }
    }

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

    public async Task<Event?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Events
            .Include(e => e.EventType)
            .Include(e => e.ParticipationRecords)
                .ThenInclude(p => p.Member)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<Event>> GetYearToDateWithParticipationAsync(int year, CancellationToken ct = default)
    {
        return await _db.Events
            .Where(e => e.Date.Year == year && e.StoredParticipationRate != null)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);
    }
}
