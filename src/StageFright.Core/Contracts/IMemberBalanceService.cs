using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Provides GL-derived balance views for members.
/// </summary>
public interface IMemberBalanceService
{
    /// <summary>Returns the outstanding GL balance for a single member.</summary>
    Task<decimal> GetBalanceAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Returns outstanding balances and fee breakdowns for all non-archived members
    /// who have a positive balance.
    /// </summary>
    Task<IReadOnlyList<MemberBalance>> GetAllMemberBalancesAsync(CancellationToken ct = default);
}
