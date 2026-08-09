using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for event type CRUD and archival.
/// System defaults (IsSystemDefault=true) cannot be archived.
/// Archive is blocked when any non-deleted Event references the type.
/// </summary>
public interface IEventTypeService
{
    /// <summary>Returns all active (non-archived) event types.</summary>
    Task<IReadOnlyList<EventType>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns all active event types selectable when scheduling a new generic event, excluding "Annual General Meeting" (FR-003).</summary>
    Task<IReadOnlyList<EventType>> GetSelectableForNewEventsAsync(CancellationToken ct = default);

    /// <summary>Returns all archived event types.</summary>
    Task<IReadOnlyList<EventType>> GetArchivedAsync(CancellationToken ct = default);

    /// <summary>Creates a new user-defined event type.</summary>
    Task<EventType> CreateAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Archives the event type.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> if IsSystemDefault=true
    /// or referenced by any non-deleted Event.
    /// </summary>
    Task ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Restores a previously archived event type.</summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);
}
