using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for Rehearsal entities.</summary>
public interface IRehearsalRepository : ISoftDeletableRepository<Rehearsal>
{
    /// <summary>Returns the most recent non-deleted rehearsal whose date is before asOf (UTC), or null.</summary>
    Task<Rehearsal?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);
}
