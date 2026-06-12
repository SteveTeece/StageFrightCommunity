using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for Event entities.</summary>
public interface IEventRepository : ISoftDeletableRepository<Event>
{
    /// <summary>Returns the most recent non-deleted event whose date is before asOf (UTC), or null.</summary>
    Task<Event?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns true if any non-deleted AGM event (EventType.Name = "Annual General Meeting") exists
    /// with a date in the given calendar year.
    /// </summary>
    Task<bool> AgmExistsInYearAsync(int year, CancellationToken ct = default);
}
