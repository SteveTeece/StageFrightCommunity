using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Organisations;

/// <summary>
/// Builds an organisation's financial history from its linked journal entries (spec 032 US3,
/// ui-contract §4.2 and §4.9). Never checks that the organisation exists: the History tab does
/// that first, and an unknown id simply has no linked entries.
/// </summary>
public class OrganisationHistoryService : IOrganisationHistoryService
{
    private readonly IJournalEntryRepository _journalRepo;

    public OrganisationHistoryService(IJournalEntryRepository journalRepo)
    {
        _journalRepo = journalRepo;
    }

    public async Task<OrganisationHistory> GetHistoryAsync(Guid organisationId, CancellationToken ct = default)
    {
        var journalEntries = await _journalRepo.GetByExternalOrganisationAsync(organisationId, ct);

        var entries = journalEntries
            .OrderByDescending(j => j.Date)
            .ThenByDescending(j => j.CreatedAt)
            .Select(ToHistoryEntry)
            .ToList();

        var totalPaid = entries.Where(e => e.Type == JournalEntryType.ExpensePayment).Sum(e => e.Amount);
        var totalReceived = entries.Where(e => e.Type == JournalEntryType.Income).Sum(e => e.Amount);

        return new OrganisationHistory(entries, totalPaid, totalReceived);
    }

    private static OrganisationHistoryEntry ToHistoryEntry(JournalEntry entry)
    {
        // The account the user chose is the entry's one line that is neither the bank nor a
        // system (tax) account: the expense account for a payment, the income account for income.
        var account = entry.Transactions
            .Select(t => t.Account)
            .FirstOrDefault(a => a is { IsBankAccount: false, IsSystem: false });

        // Every entry balances, so the debit total is the money that crossed the bank once
        // (FR-012): counting debits and credits together would double-count it.
        var amount = entry.Transactions.Sum(t => t.DebitAmount);

        return new OrganisationHistoryEntry(
            entry.Id, entry.Date, entry.Description, account?.Name ?? string.Empty, entry.Type, amount);
    }
}
