using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Append-only General Ledger repository. The GL is the single source of truth
/// for all balances, Trial Balance, aging, and report data.
/// </summary>
public interface IGLRepository
{
    /// <summary>
    /// Inserts a matched debit/credit pair atomically.
    /// Validates that debit.DebitAmount == credit.CreditAmount before inserting.
    /// </summary>
    Task AddPairAsync(Transaction debit, Transaction credit, CancellationToken ct = default);

    /// <summary>Returns the outstanding balance for the member: Σdebits − Σcredits.</summary>
    Task<decimal> GetMemberBalanceAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Returns the total outstanding balance across all members.</summary>
    Task<decimal> GetTotalOutstandingAsync(CancellationToken ct = default);

    /// <summary>Returns all GL transactions within the specified date range (inclusive).</summary>
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Returns GL transactions for a specific member within the date range.</summary>
    Task<IReadOnlyList<Transaction>> GetByMemberAsync(Guid memberId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Returns total debits and total credits for the Trial Balance within the date range.</summary>
    Task<(decimal TotalDebits, decimal TotalCredits)> GetBalanceTotalsAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Returns all GL transactions linked to a specific fee.</summary>
    Task<IReadOnlyList<Transaction>> GetByFeeAsync(Guid feeId, CancellationToken ct = default);
}
