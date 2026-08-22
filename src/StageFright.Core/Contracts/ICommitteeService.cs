using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Application service contract for committee position management.</summary>
public interface ICommitteeService
{
    /// <summary>Creates or updates the legacy committee position for the member in the given year. Position is required.</summary>
    Task<CommitteePositionRecord> AddOrUpdateAsync(Guid memberId, int year, string position, CancellationToken ct = default);

    /// <summary>Returns committee history for the member ordered by year descending.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetHistoryAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Returns committee position records under the one currently open committee term.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>Returns committee position records belonging to the given committee term.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetByTermAsync(Guid committeeTermId, CancellationToken ct = default);

    /// <summary>Returns committee position records belonging to the term started by the given AGM.</summary>
    Task<IReadOnlyList<CommitteePositionRecord>> GetByAgmAsync(Guid annualGeneralMeetingId, CancellationToken ct = default);
}
