using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for AnnualGeneralMeeting entities.</summary>
public interface IAgmRepository : ISoftDeletableRepository<AnnualGeneralMeeting>
{
    /// <summary>
    /// Returns every non-deleted AGM ordered most-recent-first — despite the name, this
    /// includes scheduled-but-not-yet-recorded AGMs (any Date, past or future); it applies no
    /// date filter beyond ordering.
    /// </summary>
    Task<IReadOnlyList<AnnualGeneralMeeting>> GetPastOrderedAsync(CancellationToken ct = default);

    /// <summary>
    /// True if a non-archived AGM already exists with a meeting Date in the given calendar
    /// year (FR-015). Archived AGMs are excluded automatically by the entity's global
    /// !IsDeleted query filter.
    /// </summary>
    Task<bool> ExistsForYearAsync(int year, CancellationToken ct = default);
}
