using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for manual bank reconciliation: draft lifecycle, tick-off of
/// cleared transactions, live difference calculation, and finalisation gating.
/// </summary>
public interface IBankReconciliationService
{
    /// <summary>Returns the account's reconciliation history, newest statement first.</summary>
    Task<IReadOnlyList<BankReconciliation>> GetHistoryAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Returns the account's in-progress draft reconciliation, or null.</summary>
    Task<BankReconciliation?> GetDraftForAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Starts a new draft reconciliation. The account must be a bank/cash account with
    /// no existing draft, and the statement date must be after the last finalised
    /// statement date for the account.
    /// </summary>
    Task<BankReconciliation> StartDraftAsync(StartReconciliationRequest request, CancellationToken ct = default);

    /// <summary>Returns the workspace view: header, account, candidate transactions, cleared total, difference.</summary>
    Task<ReconciliationWorkspaceView> GetWorkspaceAsync(Guid reconciliationId, CancellationToken ct = default);

    /// <summary>
    /// Ticks or unticks a transaction on a draft reconciliation. The transaction must
    /// belong to the reconciled account, be dated on or before the statement date, and
    /// not be cleared by any other reconciliation.
    /// </summary>
    Task ToggleClearAsync(Guid reconciliationId, Guid transactionId, CancellationToken ct = default);

    /// <summary>Finalises a draft. Requires |difference| ≤ 0.005; finalised reconciliations are immutable.</summary>
    Task FinaliseAsync(Guid reconciliationId, CancellationToken ct = default);

    /// <summary>Soft-deletes a draft reconciliation, releasing its cleared transactions.</summary>
    Task DeleteDraftAsync(Guid reconciliationId, CancellationToken ct = default);
}
