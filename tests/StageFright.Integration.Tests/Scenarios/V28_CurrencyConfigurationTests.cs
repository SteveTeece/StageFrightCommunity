using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// V28 acceptance — US1 AC-1…AC-4 (spec 028): an organisation configured with a non-Australian
/// currency (including a zero-decimal one, JPY) sees that currency's symbol and minor-unit
/// precision on every financial report, with regional grouping/placement applied to the
/// configured symbol, zero-decimal figures reconciling exactly, and an AUD dataset unchanged.
/// Real in-memory SQLite + full migrations.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class V28_CurrencyConfigurationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly DateTime PostDate = new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

    private const decimal IncomeAmount = 1_234_567m;   // whole yen
    private const decimal ExpenseAmount = 890_123m;

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
            new Account { Id = ExpenseId, Name = "Hall Hire", Type = AccountType.Expense, AccountNumber = "6000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = IncomeAmount, CreditAmount = 0m, Description = "Dues receipt", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = IncomeId, GLAccount = "4000", DebitAmount = 0m, CreditAmount = IncomeAmount, Description = "Dues income", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = ExpenseId, GLAccount = "6000", DebitAmount = ExpenseAmount, CreditAmount = 0m, Description = "Hall hire", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = 0m, CreditAmount = ExpenseAmount, Description = "Hall hire payment", CreatedAt = DateTime.UtcNow });

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Tokyo Players",
            AnnualFee = 5000m, AttendanceFee = 500m, MembershipRenewalMonth = 4,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 4, FinancialYearStartDay = 1,
            CurrencyCode = "JPY", IsTaxApplicable = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        MoneyFormatter.Configure(CurrencyCatalog.Default);
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private IEnumerable<string> AllMoneyCells(ReportData report)
    {
        foreach (var section in report.Sections)
        {
            foreach (var row in section.Rows)
                foreach (var cell in row.Cells)
                    yield return cell;
            if (section.Subtotal is not null)
                foreach (var cell in section.Subtotal.Cells)
                    yield return cell;
        }
        if (report.GrandTotal is not null)
            foreach (var cell in report.GrandTotal.Cells)
                yield return cell;
    }

    // AC-1 + AC-4: every report cell carries the configured symbol; nothing shows "$" / "AUD".
    [Fact]
    public async Task EveryFinancialReport_ShowsTheConfiguredCurrency_AndNeverDollarOrAud()
    {
        MoneyFormatter.Configure(CurrencyCatalog.Get("JPY"));

        var income = await BuildIncome().GenerateAsync(FyFilters(), TestContext.Current.CancellationToken);
        var trial = await BuildTrialBalance().GenerateAsync(FyFilters(), TestContext.Current.CancellationToken);
        var balance = await BuildBalanceSheet().GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        foreach (var report in new[] { income, trial, balance })
        {
            Assert.All(AllMoneyCells(report), cell =>
            {
                Assert.DoesNotContain("$", cell);
                Assert.DoesNotContain("AUD", cell);
            });
        }

        var duesRow = income.Sections.First(s => s.Heading == "Income").Rows.Single(r => r.Cells[0] == "Membership Dues");
        Assert.Contains("¥", duesRow.Cells[1]);
        Assert.Equal(MoneyFormatter.Format(IncomeAmount), duesRow.Cells[1]);
    }

    // AC-2: a zero-decimal currency shows no fractional digits and still reconciles exactly.
    [Fact]
    public async Task ZeroDecimalCurrency_ShowsNoFractionalDigits_AndReconciles()
    {
        MoneyFormatter.Configure(CurrencyCatalog.Get("JPY"));
        var decimalSep = CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator;

        var trial = await BuildTrialBalance().GenerateAsync(FyFilters(), TestContext.Current.CancellationToken);

        // Totals row ties (no GLBalanceException thrown) and carries no minor-unit digits.
        Assert.Equal(trial.GrandTotal!.Cells[1], trial.GrandTotal.Cells[2]);
        Assert.DoesNotContain(decimalSep + "0", trial.GrandTotal.Cells[1]);
        Assert.Equal(MoneyFormatter.Format(IncomeAmount + ExpenseAmount), trial.GrandTotal.Cells[1]);

        var (net, tax) = TaxCalculator.SplitInclusive(10_500m, 5m, minorUnitDigits: 0);
        Assert.Equal(10_500m, net + tax);
        Assert.Equal(Math.Round(10_500m * 5m / 105m, 0, MidpointRounding.AwayFromZero), tax);
    }

    // AC-3: regional grouping/placement apply, but the symbol stays the configured one.
    [Fact]
    public void RegionalFormatting_KeepsTheConfiguredSymbol_UnderAForeignCulture()
    {
        MoneyFormatter.Configure(CurrencyCatalog.Get("JPY"));
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

        var formatted = MoneyFormatter.Format(1_234_567m);
        var groupSep = CultureInfo.CurrentCulture.NumberFormat.CurrencyGroupSeparator;

        Assert.Contains("¥", formatted);
        Assert.DoesNotContain("€", formatted);
        Assert.Contains("1" + groupSep + "234" + groupSep + "567", formatted);
    }

    // AC-4: the same dataset under AUD is byte-identical to the pre-028 dollar output.
    [Fact]
    public async Task SameDataset_UnderAud_IsByteIdenticalToTheLegacyDollarString()
    {
        MoneyFormatter.Configure(CurrencyCatalog.Get("AUD"));

        var income = await BuildIncome().GenerateAsync(FyFilters(), TestContext.Current.CancellationToken);
        var duesRow = income.Sections.First(s => s.Heading == "Income").Rows.Single(r => r.Cells[0] == "Membership Dues");

        var legacy = "$" + IncomeAmount.ToString("N2", CultureInfo.CurrentCulture);
        Assert.Equal(legacy, duesRow.Cells[1]);
    }

    private IncomeStatementReportProvider BuildIncome() =>
        new(new GLRepository(_db), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private TrialBalanceReportProvider BuildTrialBalance() =>
        new(new GLRepository(_db), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private BalanceSheetReportProvider BuildBalanceSheet() =>
        new(new GLRepository(_db), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private static ReportFilterValues FyFilters()
    {
        var f = new ReportFilterValues();
        f.Set("period", "Custom");   // Income Statement honours dateFrom/dateTo only for a Custom period
        f.Set("dateFrom", "2026-03-01");
        f.Set("dateTo", "2026-03-31");
        return f;
    }

    private static ReportFilterValues AsAtFilters()
    {
        var f = new ReportFilterValues();
        f.Set("asAt", "2026-03-31");
        return f;
    }
}
