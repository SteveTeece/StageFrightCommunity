using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.InternationalAccounting;

/// <summary>
/// Spec 028 issue #354: a per-organisation <see cref="TaxEntryMode"/>. In
/// <see cref="TaxEntryMode.Exclusive"/> mode the entered figure is the net and tax is added on
/// top, so the bank line carries the gross while the income/expense line keeps the net; the
/// ledger stays balanced. <see cref="TaxEntryMode.Inclusive"/> mode is byte-identical to the
/// pre-#354 behaviour, and a freshly-migrated <c>Settings</c> row reads <c>Inclusive</c>.
/// Real in-memory SQLite, full migrations.
/// </summary>
public sealed class TaxExclusiveEntryTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
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
            new Account { Id = IncomeAccountId, Name = "Raffle Income", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Account { Id = ExpenseAccountId, Name = "Hall Hire", Type = AccountType.Expense, AccountNumber = "6000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task SettingsRow_WithoutTaxEntryModeSet_RoundTripsAsInclusive()
    {
        // A Settings row that never sets TaxEntryMode (every pre-#354 dataset) persists and
        // reloads as Inclusive through the string value-converter.
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Legacy Choir",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var loaded = await _db.Settings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TaxEntryMode.Inclusive, loaded.TaxEntryMode);
    }

    [Fact]
    public async Task ExclusiveMode_TaxableIncome_100At8Percent_PostsNet100Tax8Gross108_Balanced()
    {
        await SeedSettingsAsync(TaxEntryMode.Exclusive, taxRate: 8m);

        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 100m, AccountId = IncomeAccountId, TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);

        var lines = await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));
        Assert.Equal(108m, Assert.Single(lines, t => t.AccountId == SystemAccounts.CashId).DebitAmount);
        Assert.Equal(100m, Assert.Single(lines, t => t.AccountId == IncomeAccountId).CreditAmount);
        Assert.Equal(8m, Assert.Single(lines, t => t.AccountId == SystemAccounts.TaxCollectedId).CreditAmount);
    }

    [Fact]
    public async Task ExclusiveMode_TaxableExpense_100At8Percent_PostsNet100Tax8Gross108_Balanced()
    {
        await SeedSettingsAsync(TaxEntryMode.Exclusive, taxRate: 8m);

        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 100m,
            BankAccountId = SystemAccounts.CashId, ExpenseAccountId = ExpenseAccountId,
            TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);

        var lines = await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));
        Assert.Equal(100m, Assert.Single(lines, t => t.AccountId == ExpenseAccountId).DebitAmount);
        Assert.Equal(8m, Assert.Single(lines, t => t.AccountId == SystemAccounts.TaxPaidId).DebitAmount);
        Assert.Equal(108m, Assert.Single(lines, t => t.AccountId == SystemAccounts.CashId).CreditAmount);
    }

    [Fact]
    public async Task InclusiveMode_TaxableIncome_110At10Percent_UnchangedFromPre354()
    {
        await SeedSettingsAsync(TaxEntryMode.Inclusive, taxRate: 10m);

        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 110m, AccountId = IncomeAccountId, TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);

        var lines = await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, lines.Count);
        Assert.Equal(110m, Assert.Single(lines, t => t.AccountId == SystemAccounts.CashId).DebitAmount);
        Assert.Equal(100m, Assert.Single(lines, t => t.AccountId == IncomeAccountId).CreditAmount);
        Assert.Equal(10m, Assert.Single(lines, t => t.AccountId == SystemAccounts.TaxCollectedId).CreditAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void ExclusiveSplit_ReSumsToGrossExactly_AtEveryMinorUnit(int minorUnitDigits)
    {
        decimal[] nets = [100m, 1000.555m, 12345.678m, 0.001m, 99.999m, 7m];
        foreach (var net in nets)
        {
            var (gross, tax) = TaxCalculator.SplitExclusive(net, 8.5m, minorUnitDigits);
            Assert.Equal(gross, net + tax);
        }
    }

    // --- Helpers ---

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))),
            new JournalEntryRepository(_db), new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db), RealLocalizer.Instance);

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))),
            new JournalEntryRepository(_db), new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db), RealLocalizer.Instance);

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }

    private async Task SeedSettingsAsync(TaxEntryMode mode, decimal taxRate)
    {
        _db.Settings.Add(BuildSettings(mode, taxRate, isTaxApplicable: true));
        await _db.SaveChangesAsync();
    }

    private static Settings BuildSettings(TaxEntryMode mode, decimal? taxRate, bool isTaxApplicable) => new()
    {
        Id = Guid.NewGuid(), OrganizationName = "Test Choir",
        AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
        MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
        FinancialYearStartMonth = 7, FinancialYearStartDay = 1, CurrencyCode = "AUD",
        IsTaxApplicable = isTaxApplicable, TaxRate = taxRate, TaxEntryMode = mode,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
