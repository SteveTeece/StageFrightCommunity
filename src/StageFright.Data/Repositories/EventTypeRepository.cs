using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class EventTypeRepository : SoftDeletableBaseRepository<EventType>, IEventTypeRepository
{
    public EventTypeRepository(StageFrightDbContext db) : base(db) { }

    public async Task<bool> IsReferencedByEventsAsync(Guid eventTypeId, CancellationToken ct = default)
    {
        return await _db.Events
            .AnyAsync(e => e.EventTypeId == eventTypeId, ct);
    }
}
