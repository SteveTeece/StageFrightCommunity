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
}
