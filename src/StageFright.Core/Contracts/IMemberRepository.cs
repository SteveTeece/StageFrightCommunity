using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for Member entities.</summary>
public interface IMemberRepository : ISoftDeletableRepository<Member>
{
    /// <summary>Returns all non-deleted members with the specified status.</summary>
    Task<IReadOnlyList<Member>> GetByStatusAsync(MemberStatus status, CancellationToken ct = default);

    /// <summary>
    /// Returns members who were Active as of the given date.
    /// Query: Status=Active AND ActivateDate &lt;= date AND (InactivateDate IS NULL OR InactivateDate > date) AND IsDeleted=false.
    /// </summary>
    Task<IReadOnlyList<Member>> GetActiveAsOfAsync(DateTime date, CancellationToken ct = default);
}
