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

    /// <summary>
    /// Inserts a balanced multi-line GL set atomically. Requires at least two lines,
    /// exactly one non-zero (positive) side per line, and Σdebits == Σcredits;
    /// otherwise throws GLBalanceException and nothing is inserted.
    /// </summary>
    Task AddBalancedSetAsync(IReadOnlyList<Transaction> lines, CancellationToken ct = default);

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

    /// <summary>
    /// Returns the net balance (Σdebits − Σcredits) of a single account for all
    /// transactions dated on or before <paramref name="asAt"/>. Positive = net debit.
    /// </summary>
    Task<decimal> GetAccountBalanceAsync(Guid accountId, DateTime asAt, CancellationToken ct = default);

    /// <summary>
    /// Returns per-account debit/credit movement totals for transactions within the
    /// inclusive date range, keyed by AccountId.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)>> GetAccountMovementsAsync(
        DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Returns the account's GL transactions not yet cleared by any non-deleted bank
    /// reconciliation, optionally limited to transactions dated on or before
    /// <paramref name="upTo"/>. Ordered by date then creation time.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetUnreconciledByAccountAsync(
        Guid accountId, DateTime? upTo = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the total outstanding fee amounts grouped into standard aging buckets as of today.
    /// Buckets: Current (not yet due), 30 days (1–30 days overdue), 60 days (31–60 days overdue),
    /// 90+ days (61+ days overdue). Uses Fee.DueDate and Fee.PaidAtCreation, consistent with reports.
    /// </summary>
    Task<(decimal Current, decimal Days30, decimal Days60, decimal Days90Plus)> GetAgingBucketsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns outstanding balances (Σdebits − Σcredits on the Member Receivable account)
    /// split by FeeType, for fee-linked transactions dated on or before <paramref name="asAt"/>.
    /// Overpayment/adjustment lines (FeeId == null) are excluded from the split but still count
    /// toward per-member and total-outstanding figures elsewhere
    /// (GetMemberBalanceAsync/GetTotalOutstandingAsync).
    /// </summary>
    Task<(decimal Attendance, decimal Annual)> GetOutstandingByFeeTypeAsync(DateTime asAt, CancellationToken ct = default);

    /// <summary>
    /// Returns the number of distinct members with a positive net Member Receivable balance
    /// (Σdebits − Σcredits &gt; 0), for transactions dated on or before <paramref name="asAt"/>.
    /// Scoped to the Member Receivable account, but unlike GetOutstandingByFeeTypeAsync this
    /// includes overpayment/adjustment lines (FeeId == null) — it counts every Member
    /// Receivable transaction for a member, consistent with the authoritative per-member
    /// balance computed by GetMemberBalanceAsync.
    /// </summary>
    Task<int> GetOutstandingMemberCountAsync(DateTime asAt, CancellationToken ct = default);
}
