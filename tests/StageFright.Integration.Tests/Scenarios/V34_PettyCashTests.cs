using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V34 (spec 033, User Story 2): Cash on Hand is now Petty Cash with its number,
/// history and balance intact, and every cash entry point (member payment, rehearsal attendance fee,
/// income entry) lands there without being listed as awaiting deposit. Petty Cash stays a pay-from
/// account for expenses. Real in-memory SQLite with the full EF migration set.
/// </summary>
public sealed class V34_PettyCashTests : IAsyncLifetime
{
    private const string PreBankingMigration = "20261119195813_AddExternalOrganisations";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = CreateContext();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // --- FR-002, US2-1: the rename keeps number, history and balance -----------------

    [Fact]
    public async Task Should_ShowPettyCash1100WithFullHistoryAndUnchangedBalance_When_UpgradedFromCashOnHand_Integration()
    {
        await _db.GetService<IMigrator>().MigrateAsync(PreBankingMigration, TestContext.Current.CancellationToken);
        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO Transactions (Id, Date, AccountId, DebitAmount, CreditAmount, GLAccount, CreatedAt) VALUES
            ({Guid.NewGuid()}, '2025-09-01 00:00:00', {SystemAccounts.CashId}, '80.00', '0', '1100', '2025-09-01 00:00:00'),
            ({Guid.NewGuid()}, '2025-10-01 00:00:00', {SystemAccounts.CashId}, '0', '30.00', '1100', '2025-10-01 00:00:00');
            """, cancellationToken: TestContext.Current.CancellationToken);

        await using (var upgraded = CreateContext())
            await upgraded.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var chart = await new AccountBalanceService(new AccountRepository(_db), Gl(), NullLogger<AccountBalanceService>.Instance)
            .GetActiveAccountBalancesAsync(TestContext.Current.CancellationToken);

        var pettyCash = Assert.Single(chart, a => a.AccountId == SystemAccounts.CashId);
        Assert.Equal("Petty Cash", pettyCash.Name);
        Assert.Equal(SystemAccounts.CashNumber, pettyCash.AccountNumber);
        Assert.Equal(50m, pettyCash.Balance);
        Assert.DoesNotContain(chart, a => a.Name == "Cash on Hand");
        Assert.Equal(2, await _db.Transactions.CountAsync(t => t.AccountId == SystemAccounts.CashId, TestContext.Current.CancellationToken));
    }

    // --- FR-003, FR-004, FR-005, US2-2, US2-3: every cash entry point ---------------

    [Fact]
    public async Task Should_RaisePettyCashByTotalAndListNothingAwaiting_When_CashPaymentRehearsalFeeAndIncomeRecorded_Integration()
    {
        await SeedFreshDatabaseAsync();
        var payerId = await AddMemberAsync("Alice");
        var feeId = await SeedOutstandingFeeAsync(payerId, 50m);
        var attendeeId = await AddMemberAsync("Bob");

        await BuildPaymentService().RecordAsync(new RecordPaymentRequest
        {
            MemberId = payerId, Date = Day1, Amount = 50m, PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual, SelectedFeeIds = [feeId]
        }, TestContext.Current.CancellationToken);

        var rehearsal = await BuildRehearsalService().ScheduleAsync(
            new ScheduleRehearsalRequest { Date = Day1, Time = TimeSpan.FromHours(19) }, TestContext.Current.CancellationToken);
        await BuildAttendanceService().RecordBatchAsync(rehearsal.Id,
            [new AttendanceBatchItem { MemberId = attendeeId, Attended = true, MarkAsUnpaid = false }],
            TestContext.Current.CancellationToken);

        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day1, Amount = 75m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Cash, Description = "Raffle"
        }, TestContext.Current.CancellationToken);

        // 50 member payment + 10 attendance fee + 75 raffle.
        Assert.Equal(135m, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(0m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        var overview = await BuildBankingService().GetOverviewAsync(TestContext.Current.CancellationToken);
        Assert.Empty(overview.AwaitingItems);
        Assert.Equal(0m, overview.UndepositedTotal);
        Assert.Equal(135m, overview.PettyCashBalance);
    }

    // --- FR-002: Petty Cash is still a pay-from account for an expense ----------------

    [Fact]
    public async Task Should_OfferPettyCashAndLowerItsBalance_When_ExpensePaidFromPettyCash_Integration()
    {
        await SeedFreshDatabaseAsync();
        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day1, Amount = 75m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Cash
        }, TestContext.Current.CancellationToken);

        var payFrom = await BuildAccountService().GetBankAccountsAsync(TestContext.Current.CancellationToken);
        Assert.Contains(payFrom, a => a.Id == SystemAccounts.CashId && a.Name == "Petty Cash");

        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Day1, Amount = 20m, BankAccountId = SystemAccounts.CashId, ExpenseAccountId = ExpenseId, Payee = "Hardware shop"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(55m, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(20m, await BalanceAsync(ExpenseId));
    }

    // --- Helpers -----------------------------------------------------------------------

    private StageFrightDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<StageFrightDbContext>().UseSqlite(_connection).Options);

    private async Task SeedFreshDatabaseAsync()
    {
        await _db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var now = DateTime.UtcNow;
        _db.Accounts.AddRange(
            new Account { Id = IncomeId, Name = "Raffle Income", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now },
            new Account { Id = ExpenseId, Name = "Props", Type = AccountType.Expense, AccountNumber = "5000", SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now });
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir", AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1, CurrencyCode = "AUD", IsTaxApplicable = false,
            CreatedAt = now, UpdatedAt = now
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Debits minus credits on an account.
    private async Task<decimal> BalanceAsync(Guid accountId) =>
        await _db.Transactions.Where(t => t.AccountId == accountId).SumAsync(t => t.DebitAmount - t.CreditAmount, TestContext.Current.CancellationToken);

    private async Task<Guid> AddMemberAsync(string firstName)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = "Tester", StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return member.Id;
    }

    private async Task<Guid> SeedOutstandingFeeAsync(Guid memberId, decimal amount)
    {
        var feeId = Guid.NewGuid();
        var feeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        _db.Fees.Add(new Fee
        {
            Id = feeId, MemberId = memberId, FeeType = FeeType.Annual, Amount = amount,
            FeeDate = feeDate, DueDate = feeDate.AddYears(1).AddDays(-1), PaidAtCreation = false, CreatedAt = now
        });
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = feeDate, AccountId = SystemAccounts.MemberReceivableId, DebitAmount = amount, CreditAmount = 0m, GLAccount = SystemAccounts.MemberReceivableNumber, MemberId = memberId, FeeId = feeId, CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = feeDate, AccountId = IncomeId, DebitAmount = 0m, CreditAmount = amount, GLAccount = "4000", MemberId = null, FeeId = feeId, CreatedAt = now });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return feeId;
    }

    private GLRepository Gl() => new(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));

    private static AuditTrailService Audit() =>
        new(Substitute.For<IAuditTrailRepository>(), NullLogger<AuditTrailService>.Instance);

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance, new ExternalOrganisationRepository(_db), new ReceivedItemRepository(_db));

    private PaymentService BuildPaymentService() =>
        new(new FeeRepository(_db), new PaymentRepository(_db, Audit()), Gl(), new MemberRepository(_db), new AccountRepository(_db), new ReceivedItemRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private RehearsalService BuildRehearsalService() =>
        new(new RehearsalRepository(_db), new MemberRepository(_db), Audit(), new UnitOfWork(_db));

    private AttendanceService BuildAttendanceService() =>
        new(new RehearsalRepository(_db), new AttendanceRepository(_db), new MemberRepository(_db), new FeeRepository(_db),
            new PaymentRepository(_db, Audit()), Gl(), new AccountRepository(_db), new SettingsRepository(_db), Audit(),
            new UnitOfWork(_db), BuildRehearsalService(), RealLocalizer.Instance);

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance, new ExternalOrganisationRepository(_db));

    private AccountService BuildAccountService() =>
        new(new AccountRepository(_db), new AccountNumberAssignmentService(new AccountRepository(_db)), Audit(), new BankReconciliationRepository(_db), RealLocalizer.Instance);

    private BankingService BuildBankingService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new ReceivedItemRepository(_db), new BankDepositRepository(_db),
            new BankReconciliationRepository(_db), new ClosedPeriodGuard(new SettingsRepository(_db)), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);
}
