using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Integration.Tests.InternationalAccounting;

/// <summary>
/// Spec 028 Phase 16 / issue #355: recoverable input tax (account <c>2320</c>) is an asset, so a
/// net-refundable organisation's Balance Sheet presents it under Assets, while tax collected and
/// owed to the authority (<c>2310</c>) stays under Liabilities. Classification only — every stored
/// amount is untouched and the Trial Balance still ties. Real in-memory SQLite, full migrations.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class RecoverableInputTaxClassificationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly DateTime PostDate = InsideCurrentFy();

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.AddRange(
            new Account { Id = IncomeId, Name = "Membership Dues", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Account { Id = ExpenseId, Name = "Costumes", Type = AccountType.Expense, AccountNumber = "6000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Buys A Lot Sells A Little Inc",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1,
            CurrencyCode = "AUD", IsTaxApplicable = true, TaxRate = 10m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        MoneyFormatter.Configure(CurrencyCatalog.Get("AUD"));
    }

    public async ValueTask DisposeAsync()
    {
        MoneyFormatter.Configure(CurrencyCatalog.Default);
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Net-refundable: tax paid on purchases exceeds tax collected on sales ---

    [Fact]
    public async Task Should_PresentRecoverableTaxUnderAssets_When_TheOrgIsNetRefundable()
    {
        // Taxable purchase, $110 gross: DR Costumes 100 / DR Tax Receivable 10 / CR Cash 110.
        AddBalancedSet(
            (ExpenseId, "6000", 100m, 0m),
            (SystemAccounts.TaxPaidId, "2320", 10m, 0m),
            (SystemAccounts.CashId, "1100", 0m, 110m));
        // Smaller taxable sale, $55 gross: DR Cash 55 / CR Membership Dues 50 / CR Tax Collected 5.
        AddBalancedSet(
            (SystemAccounts.CashId, "1100", 55m, 0m),
            (IncomeId, "4000", 0m, 50m),
            (SystemAccounts.TaxCollectedId, "2310", 0m, 5m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var balance = await BalanceSheet().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var assets = balance.Sections.Single(s => s.Heading == "Assets");
        var liabilities = balance.Sections.Single(s => s.Heading == "Liabilities");

        var receivableRow = assets.Rows.Single(r => r.Cells[0] == "Tax Receivable (2320)");
        Assert.Equal(MoneyFormatter.Format(10m), receivableRow.Cells[1]);

        // The recoverable amount is on the asset side, not sitting as a negative liability.
        Assert.DoesNotContain(liabilities.Rows, r => r.Cells[0] == "Tax Receivable (2320)");
        var collectedRow = liabilities.Rows.Single(r => r.Cells[0] == "Tax Collected (2310)");
        Assert.Equal(MoneyFormatter.Format(5m), collectedRow.Cells[1]);

        // Accumulated Surplus is computed net income, so a clean statement balances by construction:
        // exactly Assets / Liabilities / Equity, with no appended out-of-balance section (FR-010).
        Assert.Equal(3, balance.Sections.Count);
    }

    // --- Net-payable: tax collected on sales exceeds tax paid on purchases ---

    [Fact]
    public async Task Should_PresentTaxOwedUnderLiabilities_When_TheOrgIsNetPayable()
    {
        // Large taxable sale, $220 gross: DR Cash 220 / CR Membership Dues 200 / CR Tax Collected 20.
        AddBalancedSet(
            (SystemAccounts.CashId, "1100", 220m, 0m),
            (IncomeId, "4000", 0m, 200m),
            (SystemAccounts.TaxCollectedId, "2310", 0m, 20m));
        // Small taxable purchase, $55 gross: DR Costumes 50 / DR Tax Receivable 5 / CR Cash 55.
        AddBalancedSet(
            (ExpenseId, "6000", 50m, 0m),
            (SystemAccounts.TaxPaidId, "2320", 5m, 0m),
            (SystemAccounts.CashId, "1100", 0m, 55m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var balance = await BalanceSheet().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var assets = balance.Sections.Single(s => s.Heading == "Assets");
        var liabilities = balance.Sections.Single(s => s.Heading == "Liabilities");

        var owedRow = liabilities.Rows.Single(r => r.Cells[0] == "Tax Collected (2310)");
        Assert.Equal(MoneyFormatter.Format(20m), owedRow.Cells[1]);

        // The small recoverable balance still sits under Assets, on the correct side.
        var receivableRow = assets.Rows.Single(r => r.Cells[0] == "Tax Receivable (2320)");
        Assert.Equal(MoneyFormatter.Format(5m), receivableRow.Cells[1]);
    }

    // --- Trial Balance still ties exactly (issue #355 acceptance) ---

    [Fact]
    public async Task Should_TieTheTrialBalance_And_ListTaxReceivableUnderAssets()
    {
        AddBalancedSet(
            (ExpenseId, "6000", 100m, 0m),
            (SystemAccounts.TaxPaidId, "2320", 10m, 0m),
            (SystemAccounts.CashId, "1100", 0m, 110m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Generation throws GLBalanceException on any debit/credit mismatch — reaching a result means it ties.
        var trial = await TrialBalance().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Equal(trial.GrandTotal!.Cells[1], trial.GrandTotal.Cells[2]);

        var assets = trial.Sections.Single(s => s.Heading == "Assets");
        Assert.Contains(assets.Rows, r => r.Cells[0] == "Tax Receivable (2320)");
        var liabilities = trial.Sections.Single(s => s.Heading == "Liabilities");
        Assert.DoesNotContain(liabilities.Rows, r => r.Cells[0] == "Tax Receivable (2320)");
    }

    // --- Helpers ---

    private void AddBalancedSet(params (Guid AccountId, string GlAccount, decimal Debit, decimal Credit)[] lines)
    {
        foreach (var (accountId, gl, debit, credit) in lines)
        {
            _db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(), Date = PostDate, AccountId = accountId, GLAccount = gl,
                DebitAmount = debit, CreditAmount = credit, Description = "spec 028 #355 fixture",
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static DateTime InsideCurrentFy()
    {
        var (from, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, 7, 1);
        return DateTime.SpecifyKind(from.AddDays(10), DateTimeKind.Utc);
    }

    private BalanceSheetReportProvider BalanceSheet() =>
        new(new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private TrialBalanceReportProvider TrialBalance() =>
        new(new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);
}
