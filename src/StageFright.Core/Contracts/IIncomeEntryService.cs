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
    /// Debit the chosen deposit bank account (default Cash on Hand 1100) / Credit the
    /// selected Income account. The account must be of type Income and must not be a
    /// system account.
    /// </summary>
    Task RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default);

    /// <summary>Returns all active (non-archived) Income accounts excluding system accounts.</summary>
    Task<IReadOnlyList<Core.Entities.Account>> GetIncomeAccountsAsync(CancellationToken ct = default);
}
