using StageFright.Core.Entities;
using StageFright.Core.Modules.Events;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for event scheduling and participation recording.
/// Events never create fees or GL entries (FR-006).
/// </summary>
public interface IEventService
{
    /// <summary>Schedules a new event.</summary>
    Task<Event> ScheduleAsync(ScheduleEventRequest request, CancellationToken ct = default);

    /// <summary>Returns all non-deleted events ordered by date descending.</summary>
    Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns the most recent non-deleted event before asOf, or null.</summary>
    Task<Event?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent past event that does not yet have participation recorded
    /// (StoredParticipationRate is null), or null if all past events have participation.
    /// </summary>
    Task<Event?> GetMostRecentPastWithoutParticipationAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent past event that has participation recorded
    /// (StoredParticipationRate is not null), or null if no past events have participation.
    /// </summary>
    Task<Event?> GetMostRecentPastWithParticipationAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Records participation for a batch of members.
    /// Computes and freezes StoredParticipationRate on the event (idempotent).
    /// No fees or GL entries are created.
    /// </summary>
    Task RecordParticipationAsync(Guid eventId, IReadOnlyList<ParticipationBatchItem> items, CancellationToken ct = default);

    /// <summary>Returns the next scheduled event on or after asOf, or null if none.</summary>
    Task<Event?> GetNextUpcomingAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>Returns true if an AGM event exists in the given calendar year.</summary>
    Task<bool> AgmExistsInYearAsync(int year, CancellationToken ct = default);

    /// <summary>Returns an event with its EventType and ParticipationRecords (including Member names) loaded, or null if not found.</summary>
    Task<Event?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all events in the given calendar year that have participation recorded,
    /// ordered by date ascending. Used for the year-to-date dashboard chart.
    /// </summary>
    Task<IReadOnlyList<Event>> GetYearToDateWithParticipationAsync(int year, CancellationToken ct = default);
}
