using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for the opening balances wizard. Posts one GL line per
/// entered account at its normal side and plugs any residual difference to the
/// Opening Balance Equity account (3100).
/// </summary>
public interface IOpeningBalanceService
{
    /// <summary>
    /// Posts the opening balances under an OpeningBalance journal entry.
    /// Zero entries are skipped; at least one non-zero entry is required.
    /// Opening balances post once only: when an OpeningBalance journal entry already
    /// exists they are locked and this throws (issue #398) — adjust with a general journal.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on bad input.
    /// </summary>
    Task RecordOpeningBalancesAsync(RecordOpeningBalancesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns all active accounts eligible for opening balances — every account
    /// except Opening Balance Equity itself, the plug target.
    /// </summary>
    Task<IReadOnlyList<Account>> GetOpeningBalanceAccountsAsync(CancellationToken ct = default);

    /// <summary>
    /// The opening balances already posted, or null when none have been. Lines from more
    /// than one OpeningBalance journal (possible only in data posted before issue #398)
    /// are summed per account, and the as-at date is the earliest entry's date.
    /// </summary>
    Task<RecordedOpeningBalances?> GetRecordedOpeningBalancesAsync(CancellationToken ct = default);

    /// <summary>
    /// Computes the residual that would be plugged to Opening Balance Equity for the
    /// given entries: positive = credit to equity, negative = debit. Pure computation
    /// for the wizard's live preview.
    /// </summary>
    decimal ComputePlug(IReadOnlyList<OpeningBalanceEntry> entries, IReadOnlyList<Account> accounts);
}
