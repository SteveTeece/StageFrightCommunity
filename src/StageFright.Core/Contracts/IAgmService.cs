using StageFright.Core.Entities;
using StageFright.Core.Modules.Agm;

namespace StageFright.Core.Contracts;

/// <summary>Application service contract for recording and reviewing Annual General Meetings.</summary>
public interface IAgmService
{
    /// <summary>Schedules an AGM (date + optional notes only); creates no attendance, elected
    /// position, or committee term (FR-001, FR-002). Rejects a second non-archived AGM in the
    /// same calendar year (FR-015).</summary>
    /// <exception cref="Exceptions.ValidationException">Another non-archived AGM already exists
    /// for request.Date's calendar year.</exception>
    Task<AnnualGeneralMeeting> ScheduleAsync(ScheduleAgmRequest request, CancellationToken ct = default);

    /// <summary>Records attendance and committee elections against a previously scheduled AGM
    /// (FR-004), updating that same row.</summary>
    /// <exception cref="Exceptions.EntityNotFoundException">agmId does not match a saved AGM.</exception>
    /// <exception cref="Exceptions.ValidationException">The AGM's Date is still in the future
    /// (FR-005), or it has already been recorded (FR-006), or a member is assigned more than one
    /// committee slot from this AGM (unchanged rule).</exception>
    Task<AnnualGeneralMeeting> RecordAsync(Guid agmId, RecordAgmRequest request, CancellationToken ct = default);

    /// <summary>Returns the AGM with the given id, or null if not found.</summary>
    Task<AnnualGeneralMeeting?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns every non-deleted AGM, most-recent-first — including scheduled-but-not-yet-recorded
    /// AGMs (any Date, past or future); it applies no date filter beyond ordering (delegates to
    /// <c>IAgmRepository.GetPastOrderedAsync</c>, which carries the same caveat despite its own
    /// name). Callers needing only genuinely past AGMs must filter by Date themselves.
    /// </summary>
    Task<IReadOnlyList<AnnualGeneralMeeting>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Archives a past AGM. Cascades to its attendance records; the committee term it started is left intact.</summary>
    Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);

    /// <summary>Records a mid-term replacement against the currently-open committee term.</summary>
    Task<CommitteePositionRecord> RecordSpecialElectionAsync(RecordSpecialElectionRequest request, CancellationToken ct = default);
}
