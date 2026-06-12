using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class EventTypeRepository : SoftDeletableBaseRepository<EventType>, IEventTypeRepository
{
    public EventTypeRepository(StageFrightDbContext db) : base(db) { }
}
