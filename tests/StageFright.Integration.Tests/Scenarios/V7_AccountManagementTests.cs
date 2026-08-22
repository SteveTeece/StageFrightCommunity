using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V7: account management.
/// Verifies GL sequential assignment, archive blocking when referenced,
/// archive of unreferenced accounts, restore, and reorder persistence.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V7_AccountManagementTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- GL sequential assignment ---

    [Fact]
    public async Task CreateIncome_FirstAccount_Gets1000()
    {
        var svc = BuildAccountService();

        var account = await svc.CreateAsync("Membership Fees", AccountType.Income, ct: TestContext.Current.CancellationToken);

        Assert.Equal("4000", account.AccountNumber);
    }

    [Fact]
    public async Task CreateIncome_SecondAccount_Gets1001()
    {
        var svc = BuildAccountService();

        await svc.CreateAsync("Membership Fees", AccountType.Income, ct: TestContext.Current.CancellationToken);
        var second = await svc.CreateAsync("Concert Tickets", AccountType.Income, ct: TestContext.Current.CancellationToken);

        Assert.Equal("4001", second.AccountNumber);
    }

    [Fact]
    public async Task CreateExpense_FirstAccount_Gets2000()
    {
        var svc = BuildAccountService();

        var account = await svc.CreateAsync("Hall Rental", AccountType.Expense, ct: TestContext.Current.CancellationToken);

        Assert.Equal("6000", account.AccountNumber);
    }

    [Fact]
    public async Task CreateExpense_SecondAccount_Gets2001()
    {
        var svc = BuildAccountService();

        await svc.CreateAsync("Hall Rental", AccountType.Expense, ct: TestContext.Current.CancellationToken);
        var second = await svc.CreateAsync("Printing", AccountType.Expense, ct: TestContext.Current.CancellationToken);

        Assert.Equal("6001", second.AccountNumber);
    }

    [Fact]
    public async Task CreateIncome_AndExpense_Independent_Sequences()
    {
        var svc = BuildAccountService();

        await svc.CreateAsync("Income A", AccountType.Income, ct: TestContext.Current.CancellationToken);
        await svc.CreateAsync("Income B", AccountType.Income, ct: TestContext.Current.CancellationToken);
        var expense = await svc.CreateAsync("Expense A", AccountType.Expense, ct: TestContext.Current.CancellationToken);

        Assert.Equal("6000", expense.AccountNumber);
    }

    // --- Archive blocked ---

    [Fact]
    public async Task Archive_AccountReferencedByTransaction_ThrowsValidationException()
    {
        var svc = BuildAccountService();

        var account = await svc.CreateAsync("Membership", AccountType.Income, ct: TestContext.Current.CancellationToken);
        await AddTransaction(account.Id, account.AccountNumber);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.ArchiveAsync(account.Id, TestContext.Current.CancellationToken));

        Assert.Contains("referenced by one or more transactions", ex.Message);
    }

    [Fact]
    public async Task Archive_AccountReferencedByTransaction_DoesNotSoftDelete()
    {
        var svc = BuildAccountService();

        var account = await svc.CreateAsync("Membership", AccountType.Income, ct: TestContext.Current.CancellationToken);
        await AddTransaction(account.Id, account.AccountNumber);

        await Assert.ThrowsAsync<ValidationException>(() => svc.ArchiveAsync(account.Id, TestContext.Current.CancellationToken));

        var inDb = await _db.Accounts.FindAsync(new object?[] { account.Id }, TestContext.Current.CancellationToken);
        Assert.False(inDb!.IsDeleted);
    }

    [Fact]
    public async Task Archive_SystemAccount_ThrowsValidationException()
    {
        var svc = BuildAccountService();
        var cashId = new Guid("00000000-0000-0000-0000-000000000001");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.ArchiveAsync(cashId, TestContext.Current.CancellationToken));

        Assert.Contains("System accounts cannot be archived", ex.Message);
    }

    // --- Archive unblocked ---

    [Fact]
    public async Task Archive_UnreferencedAccount_SoftDeletes()
    {
        var svc = BuildAccountService();

        var account = await svc.CreateAsync("Old Account", AccountType.Income, ct: TestContext.Current.CancellationToken);

        await svc.ArchiveAsync(account.Id, TestContext.Current.CancellationToken);

        var all = await svc.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(all, c => c.Id == account.Id);

        var archived = await svc.GetArchivedAsync(TestContext.Current.CancellationToken);
        Assert.Contains(archived, c => c.Id == account.Id);
    }

    [Fact]
    public async Task Archive_CreatesAuditEntry()
    {
        var svc = BuildAccountService();
        var account = await svc.CreateAsync("Old Account", AccountType.Income, ct: TestContext.Current.CancellationToken);

        await svc.ArchiveAsync(account.Id, TestContext.Current.CancellationToken);

        var audit = await _db.AuditTrailEntries.FirstOrDefaultAsync(a =>
            a.EntityType == nameof(Account) &&
            a.EntityId == account.Id &&
            a.Action == AuditAction.Delete, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(audit);
    }

    // --- Restore ---

    [Fact]
    public async Task Restore_ArchivedAccount_MakesActiveAgain()
    {
        var svc = BuildAccountService();

        var account = await svc.CreateAsync("Dormant Account", AccountType.Income, ct: TestContext.Current.CancellationToken);
        await svc.ArchiveAsync(account.Id, TestContext.Current.CancellationToken);

        await svc.RestoreAsync(account.Id, TestContext.Current.CancellationToken);

        var all = await svc.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Contains(all, c => c.Id == account.Id);
    }

    // --- Reorder ---

    [Fact]
    public async Task Reorder_PersistsSortOrder()
    {
        var svc = BuildAccountService();

        var first = await svc.CreateAsync("Account A", AccountType.Income, ct: TestContext.Current.CancellationToken);
        var second = await svc.CreateAsync("Account B", AccountType.Income, ct: TestContext.Current.CancellationToken);

        await svc.ReorderAsync(new[] { (first.Id, 5), (second.Id, 3) }, TestContext.Current.CancellationToken);

        var updatedFirst = await _db.Accounts.FindAsync(new object?[] { first.Id }, TestContext.Current.CancellationToken);
        var updatedSecond = await _db.Accounts.FindAsync(new object?[] { second.Id }, TestContext.Current.CancellationToken);

        Assert.Equal(5, updatedFirst!.SortOrder);
        Assert.Equal(3, updatedSecond!.SortOrder);
    }

    // --- Helpers ---

    private AccountService BuildAccountService()
    {
        var accountRepo = new AccountRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(_db);
        return new AccountService(accountRepo, glAssignment, auditSvc, reconciliationRepo);
    }

    private async Task AddTransaction(Guid accountId, string glAccount)
    {
        var now = DateTime.UtcNow;
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            Date = now,
            AccountId = accountId,
            DebitAmount = 50m,
            CreditAmount = 0m,
            GLAccount = glAccount,
            Description = "Test transaction",
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
    }
}
