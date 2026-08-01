using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for CommitteePositionRecord entities.</summary>
public interface ICommitteePositionRecordRepository : ISoftDeletableRepository<CommitteePositionRecord>
{
    /// <summary>Returns all non-deleted committee records for the specified member.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Returns all non-deleted legacy committee records for the given calendar year.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetByYearAsync(int year, CancellationToken ct = default);

    /// <summary>Returns all non-deleted committee records belonging to the given committee term.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetByTermAsync(Guid committeeTermId, CancellationToken ct = default);

    /// <summary>Returns all non-deleted committee records belonging to the term started by the given AGM.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetByAgmAsync(Guid annualGeneralMeetingId, CancellationToken ct = default);

    /// <summary>Returns the member's open (EndDate == null) position record in the given term, or null if they hold none.</summary>
    Task<CommitteePositionRecord?> GetOpenByMemberInTermAsync(Guid committeeTermId, Guid memberId, CancellationToken ct = default);
}
