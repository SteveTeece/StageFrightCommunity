using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository for bank deposits and their lines (spec 033). Deposits are never deleted;
/// the only change after creation is the one-time reversal link.
/// </summary>
public interface IBankDepositRepository
{
    /// <summary>Adds a deposit together with its <see cref="BankDeposit.Lines"/>.</summary>
    Task<BankDeposit> AddAsync(BankDeposit deposit, CancellationToken ct = default);

    /// <summary>
    /// Returns the deposit with its bank account, its journal entry's transactions, and its lines'
    /// received items with payer data (<c>Payment.Member</c>, <c>JournalEntry.ExternalOrganisation</c>); null when absent.
    /// </summary>
    Task<BankDeposit?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns every deposit with its bank account, newest first, for the Banking history.</summary>
    Task<IReadOnlyList<BankDeposit>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the reversal link and timestamp once. Throws <see cref="Exceptions.DataIntegrityException"/>
    /// when the deposit is already reversed, and <see cref="Exceptions.EntityNotFoundException"/> when it does not exist.
    /// </summary>
    Task MarkReversedAsync(Guid id, Guid reversalJournalEntryId, DateTime reversedAt, CancellationToken ct = default);
}
