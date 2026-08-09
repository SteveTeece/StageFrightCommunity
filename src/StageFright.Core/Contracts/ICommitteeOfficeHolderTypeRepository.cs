using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for CommitteeOfficeHolderType entities.</summary>
public interface ICommitteeOfficeHolderTypeRepository : ISoftDeletableRepository<CommitteeOfficeHolderType>
{
    /// <summary>Returns all active (non-archived) titles, built-ins first by DisplayOrder, then custom titles by DisplayOrder.</summary>
    Task<IReadOnlyList<CommitteeOfficeHolderType>> GetActiveOrderedAsync(CancellationToken ct = default);

    /// <summary>Returns the highest DisplayOrder currently used by a custom (non-built-in) title, or null if none exist.</summary>
    Task<int?> GetMaxCustomDisplayOrderAsync(CancellationToken ct = default);
}
