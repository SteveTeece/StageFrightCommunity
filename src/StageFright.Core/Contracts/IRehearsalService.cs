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
    /// Computes and freezes StoredAttendanceRate on the rehearsal.
    /// Idempotent: does nothing if the rate is already set.
    /// Formula: presentCount / (active-as-of rehearsalDate) × 100.
    /// Archived members are always excluded from the denominator.
    /// </summary>
    Task FreezeAttendanceRateAsync(Guid rehearsalId, DateTime rehearsalDate, int presentCount, CancellationToken ct = default);
}
