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
}
