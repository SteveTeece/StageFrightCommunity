using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for manually entered multi-line general journals.
/// </summary>
public interface IGeneralJournalService
{
    /// <summary>
    /// Posts the journal lines verbatim under a GeneralJournal journal entry.
    /// Requires at least two lines, exactly one positive side per line, and
    /// Σdebits == Σcredits. Lines posting to the Member Receivable account are
    /// blocked to protect per-member balance integrity.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on bad input.
    /// </summary>
    Task RecordJournalAsync(RecordJournalRequest request, CancellationToken ct = default);

    /// <summary>Returns all active accounts available for journal lines (Member Receivable excluded).</summary>
    Task<IReadOnlyList<Account>> GetJournalAccountsAsync(CancellationToken ct = default);
}
