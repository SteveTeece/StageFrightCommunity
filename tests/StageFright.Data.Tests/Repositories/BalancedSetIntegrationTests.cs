using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for GLRepository.AddBalancedSetAsync and JournalEntryRepository
/// against a real SQLite in-memory database: balanced multi-line sets commit,
/// imbalanced / one-line / both-sides sets are rejected with GLBalanceException and
/// nothing is inserted, and AddPairAsync delegates to the same validation.
/// </summary>
public sealed class BalancedSetIntegrationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private GLRepository _sut = null!;
    private JournalEntryRepository _journalRepo = null!;

    private static readonly Guid ExpenseAccountId = new("00000000-0000-0000-0000-000000000020");

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
            Id = ExpenseAccountId, Name = "Hall Hire", Type = AccountType.Expense,
            AccountNumber = "6000", SortOrder = 0, IsSystem = false,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        _sut = new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));
        _journalRepo = new JournalEntryRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- AddBalancedSetAsync: success ---

    [Fact]
    public async Task Should_CommitAllLines_When_MultiLineSetIsBalanced_Integration()
    {
        var lines = new[]
        {
            MakeLine(ExpenseAccountId, "6000", debit: 60m),
            MakeLine(ExpenseAccountId, "6000", debit: 40m),
            MakeLine(SystemAccounts.CashId, "1100", credit: 100m)
        };

        await _sut.AddBalancedSetAsync(lines, TestContext.Current.CancellationToken);

        Assert.Equal(3, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(100m, await _db.Transactions.SumAsync(t => t.DebitAmount, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(100m, await _db.Transactions.SumAsync(t => t.CreditAmount, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_LinkLinesToJournalEntry_When_JournalEntryIdSet_Integration()
    {
        var entry = await _journalRepo.AddAsync(new JournalEntry
        {
            Id = Guid.NewGuid(),
            Type = JournalEntryType.ExpensePayment,
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "Hall hire",
            CreatedAt = DateTime.UtcNow
        }, TestContext.Current.CancellationToken);

        var debit = MakeLine(ExpenseAccountId, "6000", debit: 50m);
        var credit = MakeLine(SystemAccounts.CashId, "1100", credit: 50m);
        debit.JournalEntryId = entry.Id;
        credit.JournalEntryId = entry.Id;

        await _sut.AddBalancedSetAsync(new[] { debit, credit }, TestContext.Current.CancellationToken);

        var reloaded = await _journalRepo.GetByIdAsync(entry.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.Transactions.Count);
    }

    // --- AddBalancedSetAsync: rejection + rollback ---

    [Fact]
    public async Task Should_ThrowDataAccessException_When_SaveChangesFailsPastValidation_Integration()
    {
        // Regression for #285: a balanced set (passes GLBalanceException validation) whose
        // SaveChangesAsync fails for a real DB reason (here: duplicate primary key) must
        // surface as DataAccessException, not a raw DbUpdateException, across the DAL boundary.
        var duplicateId = Guid.NewGuid();
        var debit = MakeLine(ExpenseAccountId, "6000", debit: 25m);
        var credit = MakeLine(SystemAccounts.CashId, "1100", credit: 25m);
        debit.Id = duplicateId;
        credit.Id = duplicateId;

        await Assert.ThrowsAsync<DataAccessException>(() => _sut.AddBalancedSetAsync(new[] { debit, credit }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowAndInsertNothing_When_SetIsImbalanced_Integration()
    {
        var lines = new[]
        {
            MakeLine(ExpenseAccountId, "6000", debit: 100m),
            MakeLine(SystemAccounts.CashId, "1100", credit: 90m)
        };

        await Assert.ThrowsAsync<GLBalanceException>(() => _sut.AddBalancedSetAsync(lines, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowAndInsertNothing_When_SetHasOneLine_Integration()
    {
        var lines = new[] { MakeLine(ExpenseAccountId, "6000", debit: 100m) };

        await Assert.ThrowsAsync<GLBalanceException>(() => _sut.AddBalancedSetAsync(lines, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowAndInsertNothing_When_LineHasBothSidesNonZero_Integration()
    {
        var badLine = MakeLine(ExpenseAccountId, "6000", debit: 50m);
        badLine.CreditAmount = 50m;
        var lines = new[] { badLine, MakeLine(SystemAccounts.CashId, "1100", credit: 50m) };

        await Assert.ThrowsAsync<GLBalanceException>(() => _sut.AddBalancedSetAsync(lines, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowAndInsertNothing_When_LineHasBothSidesZero_Integration()
    {
        var lines = new[]
        {
            MakeLine(ExpenseAccountId, "6000", debit: 100m),
            MakeLine(SystemAccounts.CashId, "1100", credit: 100m),
            MakeLine(SystemAccounts.CashId, "1100", credit: 0m)
        };

        await Assert.ThrowsAsync<GLBalanceException>(() => _sut.AddBalancedSetAsync(lines, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowAndInsertNothing_When_LineIsNegative_Integration()
    {
        var lines = new[]
        {
            MakeLine(ExpenseAccountId, "6000", debit: -100m),
            MakeLine(SystemAccounts.CashId, "1100", credit: -100m)
        };

        await Assert.ThrowsAsync<GLBalanceException>(() => _sut.AddBalancedSetAsync(lines, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- AddPairAsync delegation ---

    [Fact]
    public async Task Should_CommitPair_When_AddPairAsyncBalanced_Integration()
    {
        await _sut.AddPairAsync(MakeLine(ExpenseAccountId, "6000", debit: 25m), MakeLine(SystemAccounts.CashId, "1100", credit: 25m), TestContext.Current.CancellationToken);

        Assert.Equal(2, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_AddPairAsyncImbalanced_Integration()
    {
        await Assert.ThrowsAsync<GLBalanceException>(() => _sut.AddPairAsync(MakeLine(ExpenseAccountId, "6000", debit: 25m), MakeLine(SystemAccounts.CashId, "1100", credit: 30m), TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- JournalEntryRepository ---

    [Fact]
    public async Task Should_PersistJournalEntry_When_Added_Integration()
    {
        var entry = await _journalRepo.AddAsync(new JournalEntry
        {
            Id = Guid.NewGuid(),
            Type = JournalEntryType.Transfer,
            Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "Move float",
            CreatedAt = DateTime.UtcNow
        }, TestContext.Current.CancellationToken);

        var reloaded = await _journalRepo.GetByIdAsync(entry.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal(JournalEntryType.Transfer, reloaded!.Type);
        Assert.Equal("Move float", reloaded.Description);
    }

    [Fact]
    public async Task Should_ReturnNull_When_JournalEntryNotFound_Integration()
    {
        Assert.Null(await _journalRepo.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReportTypeExists_When_MatchingJournalEntryPresent_Integration()
    {
        Assert.False(await _journalRepo.AnyOfTypeAsync(JournalEntryType.OpeningBalance, TestContext.Current.CancellationToken));

        await _journalRepo.AddAsync(new JournalEntry
        {
            Id = Guid.NewGuid(),
            Type = JournalEntryType.OpeningBalance,
            Date = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow
        }, TestContext.Current.CancellationToken);

        Assert.True(await _journalRepo.AnyOfTypeAsync(JournalEntryType.OpeningBalance, TestContext.Current.CancellationToken));
        Assert.False(await _journalRepo.AnyOfTypeAsync(JournalEntryType.GeneralJournal, TestContext.Current.CancellationToken));
    }

    // --- Helpers ---

    private static Transaction MakeLine(Guid accountId, string number, decimal debit = 0m, decimal credit = 0m) => new()
    {
        Id = Guid.NewGuid(),
        Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        AccountId = accountId,
        DebitAmount = debit,
        CreditAmount = credit,
        GLAccount = number,
        Description = "Test line",
        CreatedAt = DateTime.UtcNow
    };
}
