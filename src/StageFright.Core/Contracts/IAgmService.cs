using StageFright.Core.Entities;
using StageFright.Core.Modules.Agm;

namespace StageFright.Core.Contracts;

/// <summary>Application service contract for recording and reviewing Annual General Meetings.</summary>
public interface IAgmService
{
    /// <summary>Records a complete AGM (meeting, attendance, and every election) atomically.</summary>
    Task<AnnualGeneralMeeting> RecordAsync(RecordAgmRequest request, CancellationToken ct = default);

    /// <summary>Returns the AGM with the given id, or null if not found.</summary>
    Task<AnnualGeneralMeeting?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-deleted past AGMs, most-recent-first.</summary>
    Task<IReadOnlyList<AnnualGeneralMeeting>> GetPastAsync(CancellationToken ct = default);

    /// <summary>Archives a past AGM. Cascades to its attendance records; the committee term it started is left intact.</summary>
    Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);

    /// <summary>Records a mid-term replacement against the currently-open committee term.</summary>
    Task<CommitteePositionRecord> RecordSpecialElectionAsync(RecordSpecialElectionRequest request, CancellationToken ct = default);
}
