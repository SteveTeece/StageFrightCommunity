using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Organisations;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Organisations;

/// <summary>
/// Unit tests for OrganisationHistoryService (spec 032 US3): entries newest first with total paid
/// and total received (FR-011), each entry's amount counted once from its debit lines so a taxable
/// three-line expense is not double-counted (FR-012), the chosen account picked over the bank and
/// tax lines, and an organisation with no entries getting zero totals.
/// </summary>
public class OrganisationHistoryServiceTests : TestBase
{
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly OrganisationHistoryService _sut;
    private static readonly Guid OrganisationId = Guid.NewGuid();

    private static readonly Account Bank = MakeAccount("Operating Account", AccountType.Asset, isBank: true);
    private static readonly Account HallHire = MakeAccount("Hall Hire", AccountType.Expense);
    private static readonly Account Donations = MakeAccount("Donations", AccountType.Income);
    private static readonly Account TaxPaid = MakeAccount("Tax Receivable", AccountType.Asset, isSystem: true);

    public OrganisationHistoryServiceTests()
    {
        _sut = new OrganisationHistoryService(_journalRepo);
    }

    private void GivenEntries(params JournalEntry[] entries) =>
        _journalRepo.GetByExternalOrganisationAsync(OrganisationId, Arg.Any<CancellationToken>())
            .Returns(entries.ToList());

    [Fact]
    public async Task Should_ListEntriesNewestFirstWithTotals_When_OrganisationHasPaidAndReceivedEntries()
    {
        var older = Expense(new DateTime(2026, 3, 1), 500m, "Town Hall — Hall hire");
        var newer = Income(new DateTime(2026, 4, 1), 200m, "Town Hall grant");
        GivenEntries(older, newer);

        var history = await _sut.GetHistoryAsync(OrganisationId, Ct);

        Assert.Equal(new[] { newer.Id, older.Id }, history.Entries.Select(e => e.JournalEntryId));
        Assert.Equal(500m, history.TotalPaid);
        Assert.Equal(200m, history.TotalReceived);

        var paid = history.Entries.Single(e => e.JournalEntryId == older.Id);
        Assert.Equal(JournalEntryType.ExpensePayment, paid.Type);
        Assert.Equal("Hall Hire", paid.AccountName);
        Assert.Equal("Town Hall — Hall hire", paid.Description);
        Assert.Equal(new DateTime(2026, 3, 1), paid.Date);

        var received = history.Entries.Single(e => e.JournalEntryId == newer.Id);
        Assert.Equal(JournalEntryType.Income, received.Type);
        Assert.Equal("Donations", received.AccountName);
    }

    [Fact]
    public async Task Should_OrderByCreatedAtDescending_When_EntriesShareADate()
    {
        var date = new DateTime(2026, 5, 1);
        var first = Expense(date, 10m, "first", createdAt: new DateTime(2026, 5, 1, 9, 0, 0));
        var second = Expense(date, 20m, "second", createdAt: new DateTime(2026, 5, 1, 10, 0, 0));
        GivenEntries(first, second);

        var history = await _sut.GetHistoryAsync(OrganisationId, Ct);

        Assert.Equal(new[] { second.Id, first.Id }, history.Entries.Select(e => e.JournalEntryId));
    }

    [Fact]
    public async Task Should_CountGrossOnce_When_TaxableExpenseHasThreeLines()
    {
        // DR Hall Hire 100 / DR Tax Receivable 10 / CR Bank 110 — the bank-side gross is 110.
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.ExpensePayment,
            Date = new DateTime(2026, 6, 1), Description = "Taxable hire", CreatedAt = DateTime.UtcNow,
            Transactions =
            [
                Line(TaxPaid, debit: 10m, credit: 0m),
                Line(HallHire, debit: 100m, credit: 0m),
                Line(Bank, debit: 0m, credit: 110m)
            ]
        };
        GivenEntries(entry);

        var history = await _sut.GetHistoryAsync(OrganisationId, Ct);

        Assert.Equal(110m, Assert.Single(history.Entries).Amount);
        Assert.Equal("Hall Hire", history.Entries[0].AccountName);
        Assert.Equal(110m, history.TotalPaid);
        Assert.Equal(0m, history.TotalReceived);
    }

    [Fact]
    public async Task Should_ReturnEmptyHistoryWithZeroTotals_When_OrganisationHasNoEntries()
    {
        GivenEntries();

        var history = await _sut.GetHistoryAsync(OrganisationId, Ct);

        Assert.Empty(history.Entries);
        Assert.Equal(0m, history.TotalPaid);
        Assert.Equal(0m, history.TotalReceived);
    }

    [Fact]
    public async Task Should_UseEmptyAccountName_When_EntryHasOnlyBankAndSystemLines()
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.ExpensePayment,
            Date = new DateTime(2026, 6, 1), CreatedAt = DateTime.UtcNow,
            Transactions = [Line(TaxPaid, debit: 5m, credit: 0m), Line(Bank, debit: 0m, credit: 5m)]
        };
        GivenEntries(entry);

        var history = await _sut.GetHistoryAsync(OrganisationId, Ct);

        Assert.Equal(string.Empty, Assert.Single(history.Entries).AccountName);
    }

    // --- Helpers ---

    private static JournalEntry Expense(DateTime date, decimal amount, string description, DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(), Type = JournalEntryType.ExpensePayment, Date = date,
        Description = description, CreatedAt = createdAt ?? DateTime.UtcNow,
        Transactions = [Line(HallHire, debit: amount, credit: 0m), Line(Bank, debit: 0m, credit: amount)]
    };

    private static JournalEntry Income(DateTime date, decimal amount, string description) => new()
    {
        Id = Guid.NewGuid(), Type = JournalEntryType.Income, Date = date,
        Description = description, CreatedAt = DateTime.UtcNow,
        Transactions = [Line(Bank, debit: amount, credit: 0m), Line(Donations, debit: 0m, credit: amount)]
    };

    private static Transaction Line(Account account, decimal debit, decimal credit) => new()
    {
        Id = Guid.NewGuid(), AccountId = account.Id, Account = account,
        DebitAmount = debit, CreditAmount = credit, GLAccount = account.AccountNumber,
        Date = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
    };

    private static Account MakeAccount(string name, AccountType type, bool isBank = false, bool isSystem = false) => new()
    {
        Id = isSystem ? SystemAccounts.TaxPaidId : Guid.NewGuid(), Name = name, Type = type,
        AccountNumber = "0000", IsBankAccount = isBank, IsSystem = isSystem,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
