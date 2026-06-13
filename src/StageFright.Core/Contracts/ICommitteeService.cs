using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Application service contract for committee membership management.</summary>
public interface ICommitteeService
{
    /// <summary>Creates or updates the committee position for the member in the given year. Position is required.</summary>
    Task<CommitteeMembership> AddOrUpdateAsync(Guid memberId, int year, string position, CancellationToken ct = default);

    /// <summary>Returns committee history for the member ordered by year descending.</summary>
    Task<IReadOnlyList<CommitteeMembership>> GetHistoryAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Soft-deletes all current-year committee records (annual reset, FR-031).</summary>
    Task SoftDeleteCurrentYearAsync(int year, CancellationToken ct = default);
}
