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
/// Acceptance tests for V17: bank deposits (spec 009) — Cash on Hand deposited into a bank
/// account under a distinct BankDeposit journal entry. Verifies journal-entry headers,
/// balanced GL pairs, account balance movement, and validation rejection with no persistence.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V17_BankDepositTests : IAsyncLifetime
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

    [Fact]
    public async Task RecordDeposit_CreatesDebitDestination_CreditCash_UnderBankDepositJournalEntry()
    {
        var svc = BuildBankDepositService();
        await svc.RecordDepositAsync(new RecordBankDepositRequest
        {
            Date = Today, Amount = 200m,
            ToAccountId = SavingsAccountId,
            Description = "Move float to savings"
        }, TestContext.Current.CancellationToken);

        var entry = Assert.Single(await _db.JournalEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(JournalEntryType.BankDeposit, entry.Type);

        var lines = await _db.Transactions.Where(t => t.JournalEntryId == entry.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, lines.Count);

        var debit = Assert.Single(lines, t => t.DebitAmount > 0m);
        Assert.Equal(SavingsAccountId, debit.AccountId);
        Assert.Equal(200m, debit.DebitAmount);

        var credit = Assert.Single(lines, t => t.CreditAmount > 0m);
        Assert.Equal(SystemAccounts.CashId, credit.AccountId);
        Assert.Equal(200m, credit.CreditAmount);
    }

    [Fact]
    public async Task RecordDeposit_MovesBalanceFromCashToDestination()
    {
        var svc = BuildBankDepositService();
        await svc.RecordDepositAsync(new RecordBankDepositRequest
        {
            Date = Today, Amount = 300m,
            ToAccountId = SavingsAccountId
        }, TestContext.Current.CancellationToken);

        var glRepo = new GLRepository(_db);
        Assert.Equal(-300m, await glRepo.GetAccountBalanceAsync(SystemAccounts.CashId, Today, TestContext.Current.CancellationToken));
        Assert.Equal(300m, await glRepo.GetAccountBalanceAsync(SavingsAccountId, Today, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordDeposit_ZeroAmount_ThrowsAndPersistsNothing()
    {
        var svc = BuildBankDepositService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordDepositAsync(new RecordBankDepositRequest
        {
            Date = Today, Amount = 0m,
            ToAccountId = SavingsAccountId
        }, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.JournalEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordDeposit_NonBankDestination_ThrowsAndPersistsNothing()
    {
        var svc = BuildBankDepositService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordDepositAsync(new RecordBankDepositRequest
        {
            Date = Today, Amount = 50m,
            ToAccountId = VenueHireAccountId // expense, not a bank account
        }, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.JournalEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordDeposit_DestinationEqualsCashOnHand_ThrowsAndPersistsNothing()
    {
        var svc = BuildBankDepositService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordDepositAsync(new RecordBankDepositRequest
        {
            Date = Today, Amount = 50m,
            ToAccountId = SystemAccounts.CashId
        }, TestContext.Current.CancellationToken));

        Assert.Equal(0, await _db.JournalEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- Helpers ---

    private BankDepositService BuildBankDepositService() =>
        new(new AccountRepository(_db), new GLRepository(_db), new JournalEntryRepository(_db),
            BuildAuditService(), new UnitOfWork(_db));

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }
}
