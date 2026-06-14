using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for EventType entities.</summary>
public interface IEventTypeRepository : ISoftDeletableRepository<EventType>
{
    /// <summary>
    /// Returns true if any non-deleted Event references this event type.
    /// Used to block archival when events still depend on the type.
    /// </summary>
    Task<bool> IsReferencedByEventsAsync(Guid eventTypeId, CancellationToken ct = default);
}
