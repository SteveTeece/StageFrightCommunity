using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for AnnualGeneralMeeting entities.</summary>
public interface IAgmRepository : ISoftDeletableRepository<AnnualGeneralMeeting>
{
    /// <summary>Returns all non-deleted AGMs ordered most-recent-first.</summary>
    Task<IReadOnlyList<AnnualGeneralMeeting>> GetPastOrderedAsync(CancellationToken ct = default);
}
