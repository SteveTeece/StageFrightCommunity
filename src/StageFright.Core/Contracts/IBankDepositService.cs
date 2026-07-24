using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for depositing collected Cash on Hand into a bank account.
/// </summary>
public interface IBankDepositService
{
    /// <summary>
    /// Records a bank deposit with a matching GL pair under a BankDeposit journal entry:
    /// Debit the destination bank account / Credit Cash on Hand. The destination account
    /// must be a bank account other than Cash on Hand.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on bad input,
    /// <see cref="Core.Exceptions.EntityNotFoundException"/> if the destination doesn't exist.
    /// </summary>
    Task RecordDepositAsync(RecordBankDepositRequest request, CancellationToken ct = default);
}
