using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for banking (spec 033): depositing received items and Petty Cash,
/// withdrawing cash floats, reversing deposits, and the Banking history.
/// Rule refusals throw <see cref="Exceptions.ValidationException"/> with translated text;
/// a closed period throws <see cref="Exceptions.ClosedPeriodException"/>.
/// </summary>
public interface IBankingService
{
    /// <summary>Returns the awaiting items, the Undeposited Funds and Petty Cash balances, today's cash available and the usable bank accounts.</summary>
    Task<BankingOverview> GetOverviewAsync(CancellationToken ct = default);

    /// <summary>Returns the cash that can be deposited on <paramref name="depositDate"/>: the lower of Petty Cash at the end of that day and Petty Cash now.</summary>
    Task<decimal> GetCashAvailableAsync(DateTime depositDate, CancellationToken ct = default);

    /// <summary>
    /// Posts a <c>BankDeposit</c> journal entry (DR bank total · CR Undeposited Funds cheque total · CR Petty Cash cash amount,
    /// zero lines omitted) and records the deposit and its lines in one transaction.
    /// </summary>
    Task<BankDeposit> RecordDepositAsync(RecordDepositRequest request, CancellationToken ct = default);

    /// <summary>Posts a <c>CashWithdrawal</c> journal entry: DR Petty Cash · CR the bank account.</summary>
    Task<JournalEntry> RecordWithdrawalAsync(RecordCashWithdrawalRequest request, CancellationToken ct = default);

    /// <summary>
    /// Posts the exact opposite of the deposit as a <c>BankDepositReversal</c> on the deposit's date, sets the
    /// reversal link once, and returns its items to awaiting. Refused when already reversed, when its bank line
    /// is in a finalised reconciliation, or in a closed period. A draft reconciliation line is removed.
    /// </summary>
    Task ReverseDepositAsync(Guid bankDepositId, CancellationToken ct = default);

    /// <summary>Returns deposits (including legacy deposit journal entries) and cash withdrawals, newest first.</summary>
    Task<IReadOnlyList<BankingHistoryEntry>> GetHistoryAsync(CancellationToken ct = default);

    /// <summary>Returns a deposit by its <see cref="BankDeposit"/> id, or a legacy deposit by its journal entry id; null when neither exists.</summary>
    Task<DepositDetail?> GetDepositAsync(Guid id, CancellationToken ct = default);
}
