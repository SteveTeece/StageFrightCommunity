using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository for journal entry headers. Journal entries are immutable and never
/// deleted; there are no update or delete operations.
/// </summary>
public interface IJournalEntryRepository
{
    /// <summary>Inserts a journal entry header.</summary>
    Task<JournalEntry> AddAsync(JournalEntry entry, CancellationToken ct = default);

    /// <summary>Returns the journal entry with its GL lines, or null when not found.</summary>
    Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// True when any journal entry of the given type exists — used to warn before
    /// posting opening balances a second time.
    /// </summary>
    Task<bool> AnyOfTypeAsync(JournalEntryType type, CancellationToken ct = default);

    /// <summary>
    /// Entries linked to the external organisation, with Transactions and each line's Account
    /// loaded, ordered by Date then CreatedAt descending. Never filtered by the organisation's
    /// archived state (spec 032 FR-011).
    /// </summary>
    Task<IReadOnlyList<JournalEntry>> GetByExternalOrganisationAsync(Guid externalOrganisationId, CancellationToken ct = default);

    /// <summary>
    /// Entries of any of the given types, with Transactions and each line's Account loaded (archived
    /// accounts included), ordered by Date then CreatedAt descending. Used by the Banking history for
    /// cash withdrawals and deposits posted before spec 033.
    /// </summary>
    Task<IReadOnlyList<JournalEntry>> GetByTypesAsync(IReadOnlyCollection<JournalEntryType> types, CancellationToken ct = default);
}
