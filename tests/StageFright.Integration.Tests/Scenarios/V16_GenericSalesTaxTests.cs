using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Providers;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V16: generic sales-tax-applicable income and expense postings, and
/// the Tax Summary report computed from them. Verifies the 3-line taxable posting splits
/// tax to the 2310/2320 clearing accounts, the ledger stays balanced, and the tax-collected/
/// tax-paid/total-taxable-sales rows match a $110-in/$110-out fixture at a 10% rate (tax
/// collected on sales = tax paid on purchases = $10).
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class V16_GenericSalesTaxTests : IAsyncLifetime
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
            new Account
            {
                Id = IncomeAccountId, Name = "Raffle Income",
                Type = AccountType.Income, AccountNumber = "4000",
                SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = ExpenseAccountId, Name = "Hall Hire",
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

    // --- Tax-applicable income posting ---

    [Fact]
    public async Task RecordIncome_ApplicableAndTaxable_Posts3Lines_SplitsTaxToClearingAccount()
    {
        await SeedSettingsAsync(isTaxApplicable: true);
        var svc = BuildIncomeService();

        await svc.RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 110m, AccountId = IncomeAccountId, TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);

        var lines = await _db.Transactions.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));

        var taxLine = Assert.Single(lines, t => t.AccountId == SystemAccounts.TaxCollectedId);
        Assert.Equal(10m, taxLine.CreditAmount);
        Assert.All(lines, t => Assert.Equal(TaxCode.Taxable, t.TaxCode));
    }

    // --- Tax-applicable expense posting ---

    [Fact]
    public async Task RecordExpense_ApplicableAndTaxable_Posts3Lines_SplitsTaxToClearingAccount()
    {
        await SeedSettingsAsync(isTaxApplicable: true);
        var svc = BuildExpenseService();

        await svc.RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 110m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = ExpenseAccountId,
            TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);

        var lines = await _db.Transactions.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));

        var taxLine = Assert.Single(lines, t => t.AccountId == SystemAccounts.TaxPaidId);
        Assert.Equal(10m, taxLine.DebitAmount);
        Assert.All(lines, t => Assert.Equal(TaxCode.Taxable, t.TaxCode));
    }

    // --- Toggle-off byte-identical regression ---

    [Fact]
    public async Task RecordIncome_NotApplicable_Posts2Lines_WithNullTaxCode_EvenWhenTaxCodeRequested()
    {
        await SeedSettingsAsync(isTaxApplicable: false);
        var svc = BuildIncomeService();

        await svc.RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 110m, AccountId = IncomeAccountId, TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);

        var lines = await _db.Transactions.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, t => Assert.Null(t.TaxCode));
    }

    // --- Tax Summary report ---

    [Fact]
    public async Task TaxSummary_MatchingIncomeAndExpense_TaxCollectedEqualsTaxPaidEqualsTen()
    {
        await SeedSettingsAsync(isTaxApplicable: true);

        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 110m, AccountId = IncomeAccountId, TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);
        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 110m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = ExpenseAccountId,
            TaxCode = TaxCode.Taxable
        }, TestContext.Current.CancellationToken);

        var provider = new TaxSummaryReportProvider(new GLRepository(_db), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);
        var filters = new StageFright.Reports.Models.ReportFilterValues();
        filters.Set("dateFrom", $"{Today.AddDays(-1):yyyy-MM-dd}");
        filters.Set("dateTo", $"{Today.AddDays(1):yyyy-MM-dd}");

        var result = await provider.GenerateAsync(filters, TestContext.Current.CancellationToken);

        var rows = result.Sections.Single().Rows;
        Assert.Equal(MoneyFormatter.Format(10m), rows.Single(r => r.Cells[0] == "Tax collected on sales").Cells[1]);
        Assert.Equal(MoneyFormatter.Format(10m), rows.Single(r => r.Cells[0] == "Tax paid on purchases").Cells[1]);
        Assert.Equal(MoneyFormatter.Format(110m), rows.Single(r => r.Cells[0] == "Total taxable sales").Cells[1]);
        Assert.Equal(MoneyFormatter.Format(0m), result.GrandTotal!.Cells[1]);
    }

    [Fact]
    public async Task TaxSummary_NotApplicable_SelfExplains()
    {
        await SeedSettingsAsync(isTaxApplicable: false);

        var provider = new TaxSummaryReportProvider(new GLRepository(_db), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);
        var result = await provider.GenerateAsync(new StageFright.Reports.Models.ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
        Assert.Contains("does not apply", result.SubTitle);
    }

    // --- Helpers ---

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), new GLRepository(_db), new JournalEntryRepository(_db),
            new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db), RealLocalizer.Instance);

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), new GLRepository(_db), new JournalEntryRepository(_db),
            new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db), RealLocalizer.Instance);

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }

    private async Task SeedSettingsAsync(bool isTaxApplicable)
    {
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
            MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, IsTaxApplicable = isTaxApplicable,
            TaxRate = isTaxApplicable ? 10m : null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
