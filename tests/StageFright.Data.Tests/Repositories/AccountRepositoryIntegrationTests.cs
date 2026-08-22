using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for AccountRepository — IsReferencedByTransactionsAsync,
/// GetNextAccountNumberAsync max-in-range + 1 semantics (incl. bank 1110+ and
/// archived accounts still owning their numbers), archive guard, soft-delete filter.
/// </summary>
public class AccountRepositoryIntegrationTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    // System account GUIDs (seeded by StageFrightDbContext)
    private static readonly Guid CashAccountId = new("00000000-0000-0000-0000-000000000001");

    // --- IsReferencedByTransactionsAsync ---

    [Fact]
    public async Task Should_ReturnTrue_When_TransactionReferencesAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var account = await AddAccount(db, "Membership Fees", AccountType.Income, "4000");
        await AddTransaction(db, account.Id);

        var result = await repo.IsReferencedByTransactionsAsync(account.Id, TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_NoTransactionsReferenceAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var account = await AddAccount(db, "Donations", AccountType.Income, "4000");

        var result = await repo.IsReferencedByTransactionsAsync(account.Id, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    // --- GetNextAccountNumberAsync ---

    [Fact]
    public async Task Should_Return4000_When_FirstIncomeAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var result = await repo.GetNextAccountNumberAsync(AccountType.Income, false, TestContext.Current.CancellationToken);

        Assert.Equal("4000", result);
    }

    [Fact]
    public async Task Should_Return4001_When_OneIncomeAccountExists()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        await AddAccount(db, "First Income", AccountType.Income, "4000");

        var result = await repo.GetNextAccountNumberAsync(AccountType.Income, false, TestContext.Current.CancellationToken);

        Assert.Equal("4001", result);
    }

    [Fact]
    public async Task Should_Return6000_When_FirstExpenseAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var result = await repo.GetNextAccountNumberAsync(AccountType.Expense, false, TestContext.Current.CancellationToken);

        Assert.Equal("6000", result);
    }

    [Fact]
    public async Task Should_Return6001_When_OneExpenseAccountExists()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        await AddAccount(db, "First Expense", AccountType.Expense, "6000");

        var result = await repo.GetNextAccountNumberAsync(AccountType.Expense, false, TestContext.Current.CancellationToken);

        Assert.Equal("6001", result);
    }

    [Fact]
    public async Task Should_ReturnMaxPlusOne_When_NumbersAreNonContiguous()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        await AddAccount(db, "First", AccountType.Income, "4000");
        await AddAccount(db, "Jumped", AccountType.Income, "4007");

        var result = await repo.GetNextAccountNumberAsync(AccountType.Income, false, TestContext.Current.CancellationToken);

        Assert.Equal("4008", result);
    }

    [Fact]
    public async Task Should_KeepExpenseRangeIndependent_When_IncomeAccountsExist()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        await AddAccount(db, "Income 1", AccountType.Income, "4000");
        await AddAccount(db, "Income 2", AccountType.Income, "4001");

        var result = await repo.GetNextAccountNumberAsync(AccountType.Expense, false, TestContext.Current.CancellationToken);

        Assert.Equal("6000", result);
    }

    [Fact]
    public async Task Should_ExcludeSystemAccounts_When_ComputingNextNumber()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        // Bad Debt (6999) is seeded as a system Expense; it must not push user expenses to 7000.
        var result = await repo.GetNextAccountNumberAsync(AccountType.Expense, false, TestContext.Current.CancellationToken);

        Assert.Equal("6000", result);
    }

    [Fact]
    public async Task Should_Return1110_When_FirstUserBankAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        // Cash on Hand (1100) is a seeded system bank account; user bank accounts start at 1110.
        var result = await repo.GetNextAccountNumberAsync(AccountType.Asset, true, TestContext.Current.CancellationToken);

        Assert.Equal("1110", result);
    }

    [Fact]
    public async Task Should_Return1111_When_OneUserBankAccountExists()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        await AddAccount(db, "Operating Account", AccountType.Asset, "1110", isBank: true);

        var result = await repo.GetNextAccountNumberAsync(AccountType.Asset, true, TestContext.Current.CancellationToken);

        Assert.Equal("1111", result);
    }

    [Fact]
    public async Task Should_Return1300_When_FirstNonBankAssetAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var result = await repo.GetNextAccountNumberAsync(AccountType.Asset, false, TestContext.Current.CancellationToken);

        Assert.Equal("1300", result);
    }

    [Fact]
    public async Task Should_IncludeArchivedAccounts_When_ComputingNextNumber()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var account = await AddAccount(db, "Archived Income", AccountType.Income, "4000");
        await repo.ArchiveAsync(account.Id, "system", TestContext.Current.CancellationToken);

        // Archived accounts still own their numbers (IgnoreQueryFilters).
        var result = await repo.GetNextAccountNumberAsync(AccountType.Income, false, TestContext.Current.CancellationToken);

        Assert.Equal("4001", result);
    }

    [Fact]
    public async Task Should_ThrowDataIntegrityException_When_RangeExhausted()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        await AddAccount(db, "Last Possible", AccountType.Income, "4999");

        await Assert.ThrowsAsync<DataIntegrityException>(() =>
            repo.GetNextAccountNumberAsync(AccountType.Income, false, TestContext.Current.CancellationToken));
    }

    // --- Archive guard ---

    [Fact]
    public async Task Should_ThrowValidationException_When_ArchivingSystemAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            repo.ArchiveAsync(CashAccountId, "system", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowValidationException_When_ArchivingReferencedAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var account = await AddAccount(db, "Referenced", AccountType.Income, "4000");
        await AddTransaction(db, account.Id);

        await Assert.ThrowsAsync<ValidationException>(() =>
            repo.ArchiveAsync(account.Id, "system", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_SoftDelete_When_ArchivingUnreferencedAccount()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var account = await AddAccount(db, "To Archive", AccountType.Income, "4000");

        await repo.ArchiveAsync(account.Id, "system", TestContext.Current.CancellationToken);

        var allActive = await repo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(allActive, c => c.Id == account.Id);

        var archived = await repo.GetArchivedAsync(TestContext.Current.CancellationToken);
        Assert.Contains(archived, c => c.Id == account.Id);
    }

    // --- Soft-delete filter ---

    [Fact]
    public async Task Should_ExcludeSoftDeleted_When_GettingAllAccounts()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var c1 = await AddAccount(db, "Active", AccountType.Income, "4000");
        var c2 = await AddAccount(db, "ToArchive", AccountType.Income, "4001");
        await repo.ArchiveAsync(c2.Id, "system", TestContext.Current.CancellationToken);

        var all = await repo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Contains(all, c => c.Id == c1.Id);
        Assert.DoesNotContain(all, c => c.Id == c2.Id);
    }

    // --- ReorderAsync ---

    [Fact]
    public async Task Should_UpdateSortOrder_When_Reordering()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var c1 = await AddAccount(db, "Cat A", AccountType.Income, "4000");
        var c2 = await AddAccount(db, "Cat B", AccountType.Income, "4001");

        await repo.ReorderAsync(new[] { (c1.Id, 5), (c2.Id, 3) }, TestContext.Current.CancellationToken);

        using var db2 = _factory.CreateContext();
        var updated1 = await db2.Accounts.FindAsync(new object?[] { c1.Id }, TestContext.Current.CancellationToken);
        var updated2 = await db2.Accounts.FindAsync(new object?[] { c2.Id }, TestContext.Current.CancellationToken);

        Assert.Equal(5, updated1!.SortOrder);
        Assert.Equal(3, updated2!.SortOrder);
    }

    // --- Helpers ---

    private static async Task<Account> AddAccount(
        StageFright.Data.StageFrightDbContext db,
        string name,
        AccountType type,
        string accountNumber,
        bool isBank = false)
    {
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            AccountNumber = accountNumber,
            IsSystem = false,
            IsBankAccount = isBank,
            SortOrder = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private static async Task AddTransaction(StageFright.Data.StageFrightDbContext db, Guid accountId)
    {
        var now = DateTime.UtcNow;
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            Date = now,
            AccountId = accountId,
            DebitAmount = 50m,
            CreditAmount = 0m,
            GLAccount = "1200",
            Description = "Test transaction",
            CreatedAt = now
        });
        await db.SaveChangesAsync();
    }

    public void Dispose() => _factory.Dispose();
}
