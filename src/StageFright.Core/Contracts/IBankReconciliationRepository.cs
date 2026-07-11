using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository for bank reconciliation headers and their cleared-transaction lines.
/// Enforces the draft/finalised lifecycle at the data boundary: lines can only be
/// added/removed on drafts, and finalised reconciliations can never be deleted.
/// </summary>
public interface IBankReconciliationRepository
{
    /// <summary>
    /// Creates a new draft reconciliation for the account. The opening balance is
    /// chained automatically from the previous finalised reconciliation's statement
    /// closing balance (0 for the account's first reconciliation).
    /// </summary>
    Task<BankReconciliation> CreateDraftAsync(
        Guid accountId, DateTime statementDate, decimal statementClosingBalance,
        string? notes, CancellationToken ct = default);

    /// <summary>Returns the reconciliation with its lines and account, or null.</summary>
    Task<BankReconciliation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all reconciliations for the account, newest statement first.</summary>
    Task<IReadOnlyList<BankReconciliation>> GetByAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Returns the account's single draft reconciliation, or null.</summary>
    Task<BankReconciliation?> GetDraftForAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Returns the account's most recent finalised reconciliation (by statement date), or null.</summary>
    Task<BankReconciliation?> GetLastFinalisedForAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Marks a transaction as cleared on a draft reconciliation.</summary>
    Task AddLineAsync(Guid reconciliationId, Guid transactionId, CancellationToken ct = default);

    /// <summary>Removes a cleared transaction from a draft reconciliation.</summary>
    Task RemoveLineAsync(Guid reconciliationId, Guid transactionId, CancellationToken ct = default);

    /// <summary>
    /// True when the transaction is already cleared by any non-deleted reconciliation
    /// other than the one identified by <paramref name="excludeReconciliationId"/>.
    /// </summary>
    Task<bool> IsTransactionClearedElsewhereAsync(Guid transactionId, Guid excludeReconciliationId, CancellationToken ct = default);

    /// <summary>Finalises a draft reconciliation (status + FinalisedAt). Balance gating happens in the service.</summary>
    Task FinaliseAsync(Guid reconciliationId, CancellationToken ct = default);

    /// <summary>Soft-deletes a draft reconciliation. Finalised reconciliations are undeletable.</summary>
    Task SoftDeleteDraftAsync(Guid reconciliationId, string deletedBy, CancellationToken ct = default);
}
