using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for Event entities. GetAllAsync (inherited from IRepository)
/// returns all non-deleted events ordered by date descending.
/// </summary>
public interface IEventRepository : ISoftDeletableRepository<Event>
{
    /// <summary>Returns the most recent non-deleted event whose date is before asOf (UTC), or null.</summary>
    Task<Event?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent non-deleted event whose date is before asOf (UTC)
    /// and whose participation has not yet been recorded (StoredParticipationRate is null), or null.
    /// </summary>
    Task<Event?> GetMostRecentPastWithoutParticipationAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent non-deleted event whose date is before asOf (UTC)
    /// and whose participation has been recorded (StoredParticipationRate is not null), or null.
    /// </summary>
    Task<Event?> GetMostRecentPastWithParticipationAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>Returns the earliest non-deleted event whose date is >= asOf (UTC), or null.</summary>
    Task<Event?> GetNextUpcomingAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns an event with its EventType and ParticipationRecords (including Member) loaded, or null if not found.
    /// </summary>
    Task<Event?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted events in the given calendar year that have participation recorded
    /// (StoredParticipationRate is not null), ordered by date ascending.
    /// </summary>
    Task<IReadOnlyList<Event>> GetYearToDateWithParticipationAsync(int year, CancellationToken ct = default);
}
