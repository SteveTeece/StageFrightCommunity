using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for Rehearsal entities.</summary>
public interface IRehearsalRepository : ISoftDeletableRepository<Rehearsal>
{
    /// <summary>Returns the most recent non-deleted rehearsal whose date is before asOf (UTC), or null.</summary>
    Task<Rehearsal?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent non-deleted rehearsal whose date is before asOf (UTC)
    /// and whose attendance has not yet been recorded (StoredAttendanceRate is null), or null.
    /// </summary>
    Task<Rehearsal?> GetMostRecentPastWithoutAttendanceAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent non-deleted rehearsal whose date is before asOf (UTC)
    /// and whose attendance has been recorded (StoredAttendanceRate is not null), or null.
    /// </summary>
    Task<Rehearsal?> GetMostRecentPastWithAttendanceAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>Returns the next non-deleted rehearsal on or after asOf, or null if none is scheduled.</summary>
    Task<Rehearsal?> GetNextUpcomingAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted rehearsals in the given calendar year that have attendance recorded
    /// (StoredAttendanceRate is not null), ordered by date ascending.
    /// </summary>
    Task<IReadOnlyList<Rehearsal>> GetYearToDateWithAttendanceAsync(int year, CancellationToken ct = default);
}
