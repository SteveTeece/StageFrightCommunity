using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Provides GL-derived balance views for Chart of Accounts accounts.
/// </summary>
public interface IAccountBalanceService
{
    /// <summary>
    /// Returns a balance row for every active (non-archived) account, ordered by
    /// AccountNumber. A per-account calculation failure is isolated to that row
    /// (HasError=true, Balance=null) and does not affect any other row.
    /// </summary>
    Task<IReadOnlyList<AccountBalance>> GetActiveAccountBalancesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a balance row for every archived account, ordered by AccountNumber.
    /// Same per-row error isolation as GetActiveAccountBalancesAsync.
    /// </summary>
    Task<IReadOnlyList<AccountBalance>> GetArchivedAccountBalancesAsync(CancellationToken ct = default);
}
