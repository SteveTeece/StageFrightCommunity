using StageFright.Core.Entities;
using StageFright.Core.Modules.Rehearsals;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for recording batch attendance at a rehearsal.
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Records attendance for a full batch of members in a single atomic transaction.
    /// For each attended active member: creates AttendanceRecord, Fee, and GL accrual pair.
    /// If PaidAtCreation (i.e. MarkAsUnpaid is false): additionally creates GL payment pair and Payment record.
    /// Inactive members are recorded in AttendanceRecord but produce no Fee.
    /// Idempotent per (rehearsalId, memberId): duplicate calls are silently skipped.
    /// </summary>
    Task RecordBatchAsync(Guid rehearsalId, IReadOnlyList<AttendanceBatchItem> items, CancellationToken ct = default);

    /// <summary>Returns saved attendance records for a rehearsal with Member navigation loaded, ordered by member name.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default);

    /// <summary>
    /// Returns each member's attendance-fee paid status (Fee.PaidAtCreation) for the given
    /// rehearsal, keyed by MemberId. Members with no attendance fee (e.g. did not attend, or
    /// inactive at recording time) are omitted.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, bool>> GetPaidStatusByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default);
}
