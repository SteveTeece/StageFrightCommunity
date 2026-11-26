using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for ReceivedItemRepository against a migrated SQLite in-memory database (spec 033):
/// awaiting is derived from deposit lines (a reversed deposit makes its items awaiting again), payer
/// data loads for a member (soft-deleted included), an archived organisation, a plain description and
/// the opening balance, and the one-source check and unique source indexes hold.
/// </summary>
public sealed class ReceivedItemRepositoryTests : IAsyncLifetime
{
    private static readonly Guid BankAccountId = new("33000000-0000-0000-0000-000000000110");
    private static readonly DateTime Oct1 = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private StageFrightDbContext _db = null!;
    private ReceivedItemRepository _sut = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.Add(new Account
        {
            Id = BankAccountId, Name = "Cheque Account", Type = AccountType.Asset, AccountNumber = "1110",
            IsBankAccount = true, CreatedAt = Oct1, UpdatedAt = Oct1
        });
        await _db.SaveChangesAsync();

        _sut = new ReceivedItemRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- helpers ---

    private async Task<Payment> AddPaymentAsync(string firstName, bool memberDeleted = false)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = "Smith", JoinDate = Oct1,
            IsDeleted = memberDeleted, CreatedAt = Oct1, UpdatedAt = Oct1
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(), MemberId = member.Id, Member = member, Date = Oct1, Amount = 50m,
            PaymentMethod = PaymentMethod.Check, PaymentType = PaymentType.Annual, CreatedAt = Oct1, UpdatedAt = Oct1
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return payment;
    }

    private async Task<JournalEntry> AddEntryAsync(JournalEntryType type, string? description, Guid? organisationId = null)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = type, Date = Oct1, Description = description,
            ExternalOrganisationId = organisationId, CreatedAt = Oct1
        };
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private static ReceivedItem Item(DateTime date, decimal amount, Guid? paymentId = null, Guid? journalEntryId = null) => new()
    {
        Id = Guid.NewGuid(), Date = date, Amount = amount, Method = PaymentMethod.Check,
        PaymentId = paymentId, JournalEntryId = journalEntryId, CreatedAt = date
    };

    private async Task<BankDeposit> DepositAsync(ReceivedItem item, bool reversed)
    {
        var entry = await AddEntryAsync(JournalEntryType.BankDeposit, null);
        var deposit = new BankDeposit
        {
            Id = Guid.NewGuid(), Date = Oct1, BankAccountId = BankAccountId, ChequeTotal = item.Amount,
            JournalEntryId = entry.Id, CreatedAt = Oct1,
            Lines = { new BankDepositLine { Id = Guid.NewGuid(), ReceivedItemId = item.Id, CreatedAt = Oct1 } }
        };
        if (reversed)
        {
            var reversal = await AddEntryAsync(JournalEntryType.BankDepositReversal, null);
            deposit.ReversalJournalEntryId = reversal.Id;
            deposit.ReversedAt = Oct1;
        }
        _db.BankDeposits.Add(deposit);
        await _db.SaveChangesAsync();
        return deposit;
    }

    // --- AddAsync / GetAwaitingAsync ---

    [Fact]
    public async Task Should_ReturnItemsOldestFirst_When_NoneDeposited_Integration()
    {
        var entry1 = await AddEntryAsync(JournalEntryType.Income, "Raffle");
        var entry2 = await AddEntryAsync(JournalEntryType.Income, "Donation");
        await _sut.AddAsync(Item(Oct1.AddDays(5), 20m, journalEntryId: entry1.Id), TestContext.Current.CancellationToken);
        await _sut.AddAsync(Item(Oct1, 10m, journalEntryId: entry2.Id), TestContext.Current.CancellationToken);

        var awaiting = await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken);

        Assert.Equal([10m, 20m], awaiting.Select(i => i.Amount));
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoItemsExist_Integration()
    {
        Assert.Empty(await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ExcludeItem_When_InUnreversedDeposit_Integration()
    {
        var entry = await AddEntryAsync(JournalEntryType.Income, "Raffle");
        var item = await _sut.AddAsync(Item(Oct1, 10m, journalEntryId: entry.Id), TestContext.Current.CancellationToken);
        await DepositAsync(item, reversed: false);

        Assert.Empty(await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_IncludeItemAgain_When_ItsDepositIsReversed_Integration()
    {
        var entry = await AddEntryAsync(JournalEntryType.Income, "Raffle");
        var item = await _sut.AddAsync(Item(Oct1, 10m, journalEntryId: entry.Id), TestContext.Current.CancellationToken);
        await DepositAsync(item, reversed: true);

        var awaiting = await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(item.Id, Assert.Single(awaiting).Id);
    }

    // --- payer data (research §13) ---

    [Fact]
    public async Task Should_LoadMember_When_ItemFromPaymentOfDeletedMember_Integration()
    {
        var payment = await AddPaymentAsync("Alice", memberDeleted: true);
        await _sut.AddAsync(Item(Oct1, 50m, paymentId: payment.Id), TestContext.Current.CancellationToken);

        var item = Assert.Single(await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Alice Smith", item.Payment!.Member.FullName);
    }

    [Fact]
    public async Task Should_LoadArchivedOrganisation_When_ItemFromLinkedIncome_Integration()
    {
        var organisation = new ExternalOrganisation
        {
            Id = Guid.NewGuid(), Name = "Bowling Club", IsDeleted = true, CreatedAt = Oct1, UpdatedAt = Oct1
        };
        _db.ExternalOrganisations.Add(organisation);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var entry = await AddEntryAsync(JournalEntryType.Income, "Sponsorship", organisation.Id);
        await _sut.AddAsync(Item(Oct1, 300m, journalEntryId: entry.Id), TestContext.Current.CancellationToken);

        var item = Assert.Single(await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Bowling Club", item.JournalEntry!.ExternalOrganisation!.Name);
    }

    [Fact]
    public async Task Should_LoadDescriptionOnly_When_IncomeHasNoOrganisation_Integration()
    {
        var entry = await AddEntryAsync(JournalEntryType.Income, "Cake stall");
        await _sut.AddAsync(Item(Oct1, 15m, journalEntryId: entry.Id), TestContext.Current.CancellationToken);

        var item = Assert.Single(await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken));

        Assert.Null(item.JournalEntry!.ExternalOrganisation);
        Assert.Equal("Cake stall", item.JournalEntry.Description);
    }

    [Fact]
    public async Task Should_LoadOpeningBalanceEntry_When_ItemIsOpeningBalance_Integration()
    {
        var entry = await AddEntryAsync(JournalEntryType.OpeningBalance, null);
        await _sut.AddAsync(Item(Oct1, 400m, journalEntryId: entry.Id), TestContext.Current.CancellationToken);

        var item = Assert.Single(await _sut.GetAwaitingAsync(TestContext.Current.CancellationToken));

        Assert.Equal(JournalEntryType.OpeningBalance, item.JournalEntry!.Type);
    }

    // --- GetAwaitingByIdsAsync ---

    [Fact]
    public async Task Should_ReturnOnlyAwaitingRequestedItems_When_SomeDepositedOrUnknown_Integration()
    {
        var entry1 = await AddEntryAsync(JournalEntryType.Income, "A");
        var entry2 = await AddEntryAsync(JournalEntryType.Income, "B");
        var entry3 = await AddEntryAsync(JournalEntryType.Income, "C");
        var awaiting = await _sut.AddAsync(Item(Oct1, 10m, journalEntryId: entry1.Id), TestContext.Current.CancellationToken);
        var deposited = await _sut.AddAsync(Item(Oct1, 20m, journalEntryId: entry2.Id), TestContext.Current.CancellationToken);
        await _sut.AddAsync(Item(Oct1, 30m, journalEntryId: entry3.Id), TestContext.Current.CancellationToken);
        await DepositAsync(deposited, reversed: false);

        var result = await _sut.GetAwaitingByIdsAsync(
            [awaiting.Id, deposited.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        Assert.Equal(awaiting.Id, Assert.Single(result).Id);
    }

    // --- constraints ---

    [Fact]
    public async Task Should_ThrowDataAccessException_When_BothSourcesSet_Integration()
    {
        var payment = await AddPaymentAsync("Bob");
        var entry = await AddEntryAsync(JournalEntryType.Income, "Raffle");

        await Assert.ThrowsAsync<DataAccessException>(() => _sut.AddAsync(
            Item(Oct1, 10m, paymentId: payment.Id, journalEntryId: entry.Id), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowDataAccessException_When_PaymentAlreadyHasItem_Integration()
    {
        var payment = await AddPaymentAsync("Carol");
        await _sut.AddAsync(Item(Oct1, 10m, paymentId: payment.Id), TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DataAccessException>(() => _sut.AddAsync(
            Item(Oct1, 10m, paymentId: payment.Id), TestContext.Current.CancellationToken));
    }
}
