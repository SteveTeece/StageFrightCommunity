using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Application service contract for committee office-holder title management.</summary>
public interface ICommitteeOfficeHolderTypeService
{
    /// <summary>Returns all active (non-archived) titles, built-ins first by DisplayOrder, then custom titles by DisplayOrder.</summary>
    Task<IReadOnlyList<CommitteeOfficeHolderType>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Creates a new custom office-holder title.</summary>
    Task<CommitteeOfficeHolderType> AddAsync(string name, CancellationToken ct = default);

    /// <summary>Renames a custom title. Throws ValidationException if the title is built-in.</summary>
    Task RenameAsync(Guid id, string newName, CancellationToken ct = default);

    /// <summary>Reorders custom titles among themselves. Built-in titles stay pinned at DisplayOrder 0-2.</summary>
    Task ReorderAsync(IReadOnlyList<Guid> orderedCustomTitleIds, CancellationToken ct = default);

    /// <summary>Archives a custom title. Throws ValidationException if the title is built-in.</summary>
    Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);
}
