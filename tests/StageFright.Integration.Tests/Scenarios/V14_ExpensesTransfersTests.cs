using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V14: expense payments and account transfers.
/// Verifies journal-entry headers, balanced GL pairs, account balance movement,
/// ledger-wide debit/credit equality, validation rejection, and rollback on failure.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V14_ExpensesTransfersTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid SavingsAccountId = Guid.NewGuid();
    private static readonly Guid VenueHireAccountId = Guid.NewGuid();

    private static readonly DateTime Today = DateTime.UtcNow.Date;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.AddRange(
            new Account
            {
                Id = SavingsAccountId, Name = "Savings",
                Type = AccountType.Asset, AccountNumber = "1110",
                IsBankAccount = true, SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = VenueHireAccountId, Name = "Venue Hire",
                Type = AccountType.Expense, AccountNumber = "6000",
                SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Expense payments ---

    [Fact]
    public async Task RecordExpense_CreatesDebitExpense_CreditBank_UnderJournalEntry()
    {
        var svc = BuildExpenseService();
        await svc.RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 75m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = VenueHireAccountId,
            Payee = "Town Hall", Description = "March rehearsal venue"
        }, TestContext.Current.CancellationToken);

        var entry = Assert.Single(await _db.JournalEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(JournalEntryType.ExpensePayment, entry.Type);

        var lines = await _db.Transactions.Where(t => t.JournalEntryId == entry.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, lines.Count);

        var debit = Assert.Single(lines, t => t.DebitAmount > 0m);
        Assert.Equal(VenueHireAccountId, debit.AccountId);
        Assert.Equal(75m, debit.DebitAmount);
        Assert.Equal("6000", debit.GLAccount);

        var credit = Assert.Single(lines, t => t.CreditAmount > 0m);
        Assert.Equal(SystemAccounts.CashId, credit.AccountId);
        Assert.Equal(75m, credit.CreditAmount);
        Assert.Equal(SystemAccounts.CashNumber, credit.GLAccount);
    }

    [Fact]
    public async Task RecordExpense_ReducesBankBalance_IncreasesExpenseBalance()
    {
        var svc = BuildExpenseService();
        await svc.RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 120.50m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = VenueHireAccountId
        }, TestContext.Current.CancellationToken);

        var glRepo = new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));
        Assert.Equal(-120.50m, await glRepo.GetAccountBalanceAsync(SystemAccounts.CashId, Today, TestContext.Current.CancellationToken));
        Assert.Equal(120.50m, await glRepo.GetAccountBalanceAsync(VenueHireAccountId, Today, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordExpense_NonBankPaymentAccount_ThrowsAndPersistsNothing()
    {
        var svc = BuildExpenseService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 50m,
            BankAccountId = VenueHireAccountId, // not a bank account
            ExpenseAccountId = VenueHireAccountId
        }, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.JournalEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordExpense_ZeroAmount_ThrowsAndPersistsNothing()
    {
        var svc = BuildExpenseService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 0m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = VenueHireAccountId
        }, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.JournalEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- Combined journey ---

    [Fact]
    public async Task ExpenseThenBankDeposit_LedgerDebitsEqualCredits_AndEachEntryBalances()
    {
        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 99.95m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = VenueHireAccountId
        }, TestContext.Current.CancellationToken);

        await BuildBankDepositService().RecordDepositAsync(new RecordBankDepositRequest
        {
            Date = Today, Amount = 250m,
            ToAccountId = SavingsAccountId
        }, TestContext.Current.CancellationToken);

        var totalDebits = await _db.Transactions.SumAsync(t => t.DebitAmount, cancellationToken: TestContext.Current.CancellationToken);
        var totalCredits = await _db.Transactions.SumAsync(t => t.CreditAmount, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(totalDebits, totalCredits);

        var entries = await _db.JournalEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, entries.Count);
        foreach (var entry in entries)
        {
            var lines = await _db.Transactions.Where(t => t.JournalEntryId == entry.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));
        }
    }

    // --- Helpers ---

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new JournalEntryRepository(_db),
            new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db), RealLocalizer.Instance);

    private BankDepositService BuildBankDepositService() =>
        new(new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new JournalEntryRepository(_db),
            BuildAuditService(), new UnitOfWork(_db), RealLocalizer.Instance);

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }
}
