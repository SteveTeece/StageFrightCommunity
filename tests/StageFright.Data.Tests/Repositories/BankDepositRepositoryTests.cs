using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for BankDepositRepository against a migrated SQLite in-memory database (spec 033):
/// a deposit saves with its lines and loads with its account, GL lines and payer data; history is newest
/// first; the reversal link is write-once; and the (deposit, item) unique index allows re-depositing an
/// item on a new deposit after its first deposit is reversed.
/// </summary>
public sealed class BankDepositRepositoryTests : IAsyncLifetime
{
    private static readonly Guid BankAccountId = new("33000000-0000-0000-0000-000000000110");
    private static readonly DateTime Oct1 = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private StageFrightDbContext _db = null!;
    private BankDepositRepository _sut = null!;

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

        _sut = new BankDepositRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- helpers ---

    private async Task<JournalEntry> AddDepositEntryAsync(decimal amount, DateTime date)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.BankDeposit, Date = date, CreatedAt = date,
            Transactions =
            {
                new Transaction
                {
                    Id = Guid.NewGuid(), Date = date, AccountId = BankAccountId, DebitAmount = amount,
                    GLAccount = "1110", CreatedAt = date
                },
                new Transaction
                {
                    Id = Guid.NewGuid(), Date = date, AccountId = SystemAccounts.UndepositedFundsId, CreditAmount = amount,
                    GLAccount = SystemAccounts.UndepositedFundsNumber, CreatedAt = date
                }
            }
        };
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private async Task<ReceivedItem> AddMemberChequeAsync(decimal amount)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Dana", LastName = "Jones", JoinDate = Oct1, CreatedAt = Oct1, UpdatedAt = Oct1
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(), MemberId = member.Id, Member = member, Date = Oct1, Amount = amount,
            PaymentMethod = PaymentMethod.Check, PaymentType = PaymentType.Annual, CreatedAt = Oct1, UpdatedAt = Oct1
        };
        var item = new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = Oct1, Amount = amount, Method = PaymentMethod.Check, Reference = "000123",
            PaymentId = payment.Id, CreatedAt = Oct1
        };
        _db.Payments.Add(payment);
        _db.ReceivedItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<BankDeposit> NewDepositAsync(DateTime date, decimal cheques, params Guid[] itemIds)
    {
        var entry = await AddDepositEntryAsync(cheques, date);
        var deposit = new BankDeposit
        {
            Id = Guid.NewGuid(), Date = date, BankAccountId = BankAccountId, ChequeTotal = cheques,
            JournalEntryId = entry.Id, CreatedAt = date
        };
        foreach (var itemId in itemIds)
            deposit.Lines.Add(new BankDepositLine { Id = Guid.NewGuid(), ReceivedItemId = itemId, CreatedAt = date });
        return deposit;
    }

    // --- AddAsync / GetByIdAsync ---

    [Fact]
    public async Task Should_LoadDepositWithAccountLinesAndPayer_When_Added_Integration()
    {
        var item = await AddMemberChequeAsync(80m);
        var deposit = await _sut.AddAsync(await NewDepositAsync(Oct1, 80m, item.Id), TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var loaded = await _sut.GetByIdAsync(deposit.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal("Cheque Account", loaded.BankAccount!.Name);
        Assert.Equal(2, loaded.JournalEntry!.Transactions.Count);
        var line = Assert.Single(loaded.Lines);
        Assert.Equal("000123", line.ReceivedItem!.Reference);
        Assert.Equal("Dana Jones", line.ReceivedItem.Payment!.Member.FullName);
        Assert.Equal(80m, loaded.Total);
    }

    [Fact]
    public async Task Should_ReturnNull_When_DepositDoesNotExist_Integration()
    {
        Assert.Null(await _sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    // --- GetAllAsync ---

    [Fact]
    public async Task Should_ReturnDepositsNewestFirst_When_SeveralExist_Integration()
    {
        await _sut.AddAsync(await NewDepositAsync(Oct1, 10m), TestContext.Current.CancellationToken);
        await _sut.AddAsync(await NewDepositAsync(Oct1.AddDays(7), 20m), TestContext.Current.CancellationToken);

        var all = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal([20m, 10m], all.Select(d => d.ChequeTotal));
        Assert.All(all, d => Assert.Equal("Cheque Account", d.BankAccount!.Name));
    }

    // --- MarkReversedAsync ---

    [Fact]
    public async Task Should_SetReversalLink_When_NotYetReversed_Integration()
    {
        var deposit = await _sut.AddAsync(await NewDepositAsync(Oct1, 10m), TestContext.Current.CancellationToken);
        var reversal = await AddDepositEntryAsync(10m, Oct1);
        var reversedAt = Oct1.AddDays(2);

        await _sut.MarkReversedAsync(deposit.Id, reversal.Id, reversedAt, TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var loaded = await _sut.GetByIdAsync(deposit.Id, TestContext.Current.CancellationToken);
        Assert.Equal(reversal.Id, loaded!.ReversalJournalEntryId);
        Assert.Equal(reversedAt, loaded.ReversedAt);
    }

    [Fact]
    public async Task Should_ThrowDataIntegrityException_When_AlreadyReversed_Integration()
    {
        var deposit = await _sut.AddAsync(await NewDepositAsync(Oct1, 10m), TestContext.Current.CancellationToken);
        var reversal = await AddDepositEntryAsync(10m, Oct1);
        await _sut.MarkReversedAsync(deposit.Id, reversal.Id, Oct1, TestContext.Current.CancellationToken);
        var second = await AddDepositEntryAsync(10m, Oct1);

        await Assert.ThrowsAsync<DataIntegrityException>(() =>
            _sut.MarkReversedAsync(deposit.Id, second.Id, Oct1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFoundException_When_ReversingUnknownDeposit_Integration()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _sut.MarkReversedAsync(Guid.NewGuid(), Guid.NewGuid(), Oct1, TestContext.Current.CancellationToken));
    }

    // --- line uniqueness ---

    [Fact]
    public async Task Should_ThrowDataAccessException_When_SameItemTwiceOnOneDeposit_Integration()
    {
        var item = await AddMemberChequeAsync(80m);
        var deposit = await NewDepositAsync(Oct1, 160m, item.Id, item.Id);

        await Assert.ThrowsAsync<DataAccessException>(() => _sut.AddAsync(deposit, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_AllowItemOnNewDeposit_When_FirstDepositReversed_Integration()
    {
        var item = await AddMemberChequeAsync(80m);
        var first = await _sut.AddAsync(await NewDepositAsync(Oct1, 80m, item.Id), TestContext.Current.CancellationToken);
        var reversal = await AddDepositEntryAsync(80m, Oct1);
        await _sut.MarkReversedAsync(first.Id, reversal.Id, Oct1, TestContext.Current.CancellationToken);

        var second = await _sut.AddAsync(await NewDepositAsync(Oct1.AddDays(1), 80m, item.Id), TestContext.Current.CancellationToken);

        Assert.Equal(2, await _db.BankDepositLines.CountAsync(l => l.ReceivedItemId == item.Id, TestContext.Current.CancellationToken));
        Assert.NotEqual(first.Id, second.Id);
    }
}
