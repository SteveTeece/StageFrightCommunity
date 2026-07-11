using StageFright.Core.Entities;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Everything the reconciliation workspace page needs: the reconciliation header,
/// its account, the candidate transactions with cleared state, and the live totals.
/// </summary>
public class ReconciliationWorkspaceView
{
    /// <summary>The reconciliation being worked on.</summary>
    public required BankReconciliation Reconciliation { get; init; }

    /// <summary>The bank/cash account being reconciled.</summary>
    public required Account Account { get; init; }

    /// <summary>
    /// Candidate transactions dated on or before the statement date: those cleared in
    /// this reconciliation plus those not cleared by any other reconciliation.
    /// </summary>
    public required IReadOnlyList<ReconciliationTransactionView> Transactions { get; init; }

    /// <summary>Net cleared movement: Σ(cleared debits) − Σ(cleared credits).</summary>
    public decimal ClearedTotal { get; init; }

    /// <summary>
    /// StatementClosingBalance − (OpeningBalance + ClearedTotal).
    /// Finalisation requires |Difference| ≤ 0.005.
    /// </summary>
    public decimal Difference { get; init; }
}
