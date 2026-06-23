using StageFright.Core.Entities;
using StageFright.Core.Modules.Rehearsals;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for rehearsal scheduling and attendance rate management.
/// </summary>
public interface IRehearsalService
{
    Task<Rehearsal> ScheduleAsync(ScheduleRehearsalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<Rehearsal>> GetAllAsync(CancellationToken ct = default);
    Task<Rehearsal?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent past rehearsal that does not yet have attendance recorded
    /// (StoredAttendanceRate is null), or null if all past rehearsals have attendance.
    /// </summary>
    Task<Rehearsal?> GetMostRecentPastWithoutAttendanceAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent past rehearsal that has attendance recorded
    /// (StoredAttendanceRate is not null), or null if no past rehearsals have attendance.
    /// </summary>
    Task<Rehearsal?> GetMostRecentPastWithAttendanceAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Computes and freezes StoredAttendanceRate on the rehearsal.
    /// Idempotent: does nothing if the rate is already set.
    /// Formula: presentCount / (active-as-of rehearsalDate) × 100.
    /// Archived members are always excluded from the denominator.
    /// </summary>
    Task FreezeAttendanceRateAsync(Guid rehearsalId, DateTime rehearsalDate, int presentCount, CancellationToken ct = default);
}
