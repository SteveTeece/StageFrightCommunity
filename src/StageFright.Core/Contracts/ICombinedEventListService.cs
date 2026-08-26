using StageFright.Core.Modules.Events;

namespace StageFright.Core.Contracts;

/// <summary>
/// Read-only merge contract for the All Events screen: combines every non-archived Event and
/// non-archived AnnualGeneralMeeting into one date-sorted list (spec 023, FR-001/FR-002).
/// </summary>
public interface ICombinedEventListService
{
    /// <summary>
    /// Returns every non-archived Event and non-archived AnnualGeneralMeeting as one list of
    /// CombinedEventListItem, sorted by Date descending (FR-001, FR-002). Read-only — creates,
    /// updates, or deletes nothing.
    /// </summary>
    Task<IReadOnlyList<CombinedEventListItem>> GetAllAsync(CancellationToken ct = default);
}
