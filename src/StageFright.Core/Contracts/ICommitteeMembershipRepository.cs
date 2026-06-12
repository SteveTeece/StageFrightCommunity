using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for CommitteeMembership entities.</summary>
public interface ICommitteeMembershipRepository : ISoftDeletableRepository<CommitteeMembership>
{
    /// <summary>Returns all non-deleted committee records for the specified member.</summary>
    Task<IReadOnlyList<CommitteeMembership>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Returns all non-deleted committee records for the given calendar year.</summary>
    Task<IReadOnlyList<CommitteeMembership>> GetByYearAsync(int year, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes all committee membership records for the specified year (annual reset FR-031).
    /// Sets IsDeleted=true, DeletedAt=UtcNow, DeletedBy on every matching record.
    /// </summary>
    Task SoftDeleteCurrentYearAsync(int year, string deletedBy, CancellationToken ct = default);
}
