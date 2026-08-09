using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for CommitteeTerm entities.</summary>
public interface ICommitteeTermRepository : IRepository<CommitteeTerm>
{
    /// <summary>Returns the currently open term (EndDate == null), or null if none exists.</summary>
    Task<CommitteeTerm?> GetOpenAsync(CancellationToken ct = default);
}
