using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.InternationalAccounting;

/// <summary>
/// Spec 028 US6 (FR-016 / FR-017, SC-009): once <c>Settings.ClosedThroughDate</c> is set, every
/// finance posting path — funnelled through <see cref="GLRepository"/> and the
/// <see cref="ClosedPeriodGuard"/> — rejects a transaction dated on or before it and leaves no
/// partial record (no <c>JournalEntry</c>, <c>Transaction</c>, <c>Payment</c> or <c>Fee</c> row),
/// while a later-dated transaction posts normally. Real in-memory SQLite, full EF migration set.
/// </summary>
public sealed class ClosedPeriodLockTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();

    // Everything on or before this date is closed.
    private static readonly DateTime ClosedThrough = new(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InsideClosed = ClosedThrough;                 // exactly on the boundary
    private static readonly DateTime BeforeClosed = ClosedThrough.AddDays(-40);    // well inside
    private static readonly DateTime AfterClosed = ClosedThrough.AddDays(1);       // first open day

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();   // seeds the system accounts (Cash 1100, Member Receivable, Bad Debt, …)

        _db.Accounts.AddRange(
            new Account { Id = IncomeId, Name = "Raffle Income", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Account { Id = ExpenseId, Name = "Hall Hire", Type = AccountType.Expense, AccountNumber = "6000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Account { Id = BankId, Name = "Community Bank", Type = AccountType.Asset, AccountNumber = "1110", IsBankAccount = true, SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1,
            CurrencyCode = "AUD", IsTaxApplicable = false,
            ClosedThroughDate = null,   // set per test
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private async Task CloseThroughAsync(DateTime? date)
    {
        var s = await _db.Settings.FirstAsync(TestContext.Current.CancellationToken);
        s.ClosedThroughDate = date;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // --- Manual journal / expense / income / bank deposit: explicit posting date ----------

    public static TheoryData<string> DateControlledPaths() => new() { "journal", "expense", "income", "deposit" };

    [Theory]
    [MemberData(nameof(DateControlledPaths))]
    public async Task Should_RejectAndPersistNothing_When_PostingIsDatedInTheClosedPeriod(string path)
    {
        await CloseThroughAsync(ClosedThrough);

        await Assert.ThrowsAsync<ClosedPeriodException>(() => PostAsync(path, InsideClosed));

        Assert.Empty(await _db.JournalEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(DateControlledPaths))]
    public async Task Should_Post_When_DatedAfterTheClosedPeriod(string path)
    {
        await CloseThroughAsync(ClosedThrough);

        await PostAsync(path, AfterClosed);   // must not throw

        Assert.Single(await _db.JournalEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await _db.Transactions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_PostAnyBackdatedEntry_When_NoPeriodIsClosed()
    {
        // Control: with ClosedThroughDate null the guard is a no-op, so even a deeply
        // back-dated entry posts normally.
        await CloseThroughAsync(null);

        await PostAsync("journal", BeforeClosed);

        Assert.Single(await _db.JournalEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    private Task PostAsync(string path, DateTime date) => path switch
    {
        "journal" => BuildJournalService().RecordJournalAsync(new RecordJournalRequest
        {
            Date = date,
            Description = "Manual journal",
            Lines =
            [
                new JournalLine { AccountId = SystemAccounts.CashId, DebitAmount = 20m, CreditAmount = 0m },
                new JournalLine { AccountId = IncomeId, DebitAmount = 0m, CreditAmount = 20m },
            ]
        }, TestContext.Current.CancellationToken),

        "expense" => BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = date, Amount = 30m,
            BankAccountId = SystemAccounts.CashId, ExpenseAccountId = ExpenseId
        }, TestContext.Current.CancellationToken),

        "income" => BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = date, Amount = 40m, AccountId = IncomeId
        }, TestContext.Current.CancellationToken),

        "deposit" => BuildBankDepositService().RecordDepositAsync(new RecordBankDepositRequest
        {
            Date = date, Amount = 25m, ToAccountId = BankId
        }, TestContext.Current.CancellationToken),

        _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
    };

    // --- Member payment: pair posting through AddPairAsync -------------------------------

    [Fact]
    public async Task Should_RejectPaymentAndPersistNothing_When_DatedInTheClosedPeriod()
    {
        var memberId = await AddMemberAsync();
        await SeedOutstandingFeeAsync(memberId, 50m);   // 1 Fee + 2 GL rows
        await CloseThroughAsync(ClosedThrough);

        await Assert.ThrowsAsync<ClosedPeriodException>(() =>
            BuildPaymentService().RecordAsync(new RecordPaymentRequest
            {
                MemberId = memberId, Date = InsideClosed, Amount = 50m
            }, TestContext.Current.CancellationToken));

        Assert.Empty(await _db.Payments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await _db.Transactions.CountAsync(TestContext.Current.CancellationToken)); // only the seeded accrual
    }

    [Fact]
    public async Task Should_RecordPayment_When_DatedAfterTheClosedPeriod()
    {
        var memberId = await AddMemberAsync();
        await SeedOutstandingFeeAsync(memberId, 50m);
        await CloseThroughAsync(ClosedThrough);

        await BuildPaymentService().RecordAsync(new RecordPaymentRequest
        {
            MemberId = memberId, Date = AfterClosed, Amount = 50m
        }, TestContext.Current.CancellationToken);

        Assert.Single(await _db.Payments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(4, await _db.Transactions.CountAsync(TestContext.Current.CancellationToken)); // accrual + payment pair
    }

    // --- Fee accrual: FeeService dates the accrual at 1 January of the current year ------

    [Fact]
    public async Task Should_RejectFeeAccrualAndPersistNothing_When_TheCurrentYearIsClosed()
    {
        var memberId = await AddMemberAsync();
        await CloseThroughAsync(DateTime.UtcNow.Date);   // today and everything before it is closed

        await Assert.ThrowsAsync<ClosedPeriodException>(() =>
            BuildFeeService().ApplyAnnualFeesAsync([memberId], TestContext.Current.CancellationToken));

        Assert.Empty(await _db.Fees.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_AccrueFee_When_TheCurrentYearIsOpen()
    {
        var memberId = await AddMemberAsync();
        await CloseThroughAsync(new DateTime(2000, 1, 1));   // long-closed prior period only

        await BuildFeeService().ApplyAnnualFeesAsync([memberId], TestContext.Current.CancellationToken);

        Assert.Single(await _db.Fees.ToListAsync(TestContext.Current.CancellationToken));
    }

    // --- Reactivation forgiveness: write-off dated "now" --------------------------------

    [Fact]
    public async Task Should_RejectForgivenessAndPersistNothing_When_TodayIsClosed()
    {
        var memberId = await AddMemberAsync();
        var feeId = await SeedOutstandingFeeAsync(memberId, 50m);
        await CloseThroughAsync(DateTime.UtcNow.Date.AddDays(1));   // now is inside the closed period

        await Assert.ThrowsAsync<ClosedPeriodException>(() =>
            BuildForgivenessService().ApplyForgivenessAsync(memberId, [feeId], TestContext.Current.CancellationToken));

        Assert.Equal(2, await _db.Transactions.CountAsync(TestContext.Current.CancellationToken)); // only the seeded accrual — no write-off lines
    }

    [Fact]
    public async Task Should_ApplyForgiveness_When_TodayIsOpen()
    {
        var memberId = await AddMemberAsync();
        var feeId = await SeedOutstandingFeeAsync(memberId, 50m);
        await CloseThroughAsync(new DateTime(2000, 1, 1));

        await BuildForgivenessService().ApplyForgivenessAsync(memberId, [feeId], TestContext.Current.CancellationToken);

        Assert.Equal(4, await _db.Transactions.CountAsync(TestContext.Current.CancellationToken)); // accrual + write-off pair
    }

    // --- Seeding helpers --------------------------------------------------------------

    private async Task<Guid> AddMemberAsync()
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Alex", StreetAddress = "1 Test St",
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
        var feeDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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

    // --- Service builders -----------------------------------------------------------

    private GLRepository Gl() => new(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));

    private static AuditTrailService Audit() =>
        new(Substitute.For<IAuditTrailRepository>(), NullLogger<AuditTrailService>.Instance);

    private GeneralJournalService BuildJournalService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private BankDepositService BuildBankDepositService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private PaymentService BuildPaymentService() =>
        new(new FeeRepository(_db), new PaymentRepository(_db, Audit()), Gl(), new MemberRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private FeeService BuildFeeService() =>
        new(new MemberRepository(_db), new FeeRepository(_db), Gl(), new AccountRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private ReactivationForgivenessService BuildForgivenessService() =>
        new(new FeeRepository(_db), Gl(), new MemberRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db));
}
