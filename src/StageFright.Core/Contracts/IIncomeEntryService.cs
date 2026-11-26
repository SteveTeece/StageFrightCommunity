using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for recording non-member income such as raffles,
/// fundraising events, donations, and other miscellaneous income sources.
/// </summary>
public interface IIncomeEntryService
{
    /// <summary>
    /// Records an income entry with a matching GL pair under an Income journal entry:
    /// Debit where the payment method routes it (cash to Petty Cash 1100, cheque and other to
    /// Undeposited Funds 1105 as a received item, card and transfer to the chosen bank) / Credit the
    /// selected Income account. The account must be of type Income and must not be a
    /// system account.
    /// </summary>
    Task RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default);

    /// <summary>Returns all active (non-archived) Income accounts excluding system accounts.</summary>
    Task<IReadOnlyList<Core.Entities.Account>> GetIncomeAccountsAsync(CancellationToken ct = default);
}
