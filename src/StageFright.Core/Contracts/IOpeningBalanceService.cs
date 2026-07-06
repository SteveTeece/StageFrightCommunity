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
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on bad input.
    /// </summary>
    Task RecordOpeningBalancesAsync(RecordOpeningBalancesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns all active accounts eligible for opening balances — excludes
    /// Member Receivable, the GST clearing accounts, and Opening Balance Equity itself.
    /// </summary>
    Task<IReadOnlyList<Account>> GetOpeningBalanceAccountsAsync(CancellationToken ct = default);

    /// <summary>True when an OpeningBalance journal entry already exists (the wizard shows a warning).</summary>
    Task<bool> HasExistingOpeningBalancesAsync(CancellationToken ct = default);

    /// <summary>
    /// Computes the residual that would be plugged to Opening Balance Equity for the
    /// given entries: positive = credit to equity, negative = debit. Pure computation
    /// for the wizard's live preview.
    /// </summary>
    decimal ComputePlug(IReadOnlyList<OpeningBalanceEntry> entries, IReadOnlyList<Account> accounts);
}
