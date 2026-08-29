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

namespace StageFright.Integration.Tests.InternationalAccounting;

/// <summary>
/// Spec 028 zero-drift regression (SC-004, FR-006, FR-031): a pre-existing <c>AUD</c> dataset
/// must, after the configurable-currency change, produce reports whose figures are the same
/// numbers as before — now carrying a <c>"$"</c> — and must not alter a single stored monetary
/// value. Uses a real in-memory SQLite database with the full EF migration set.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class AudZeroDriftTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly DateTime PostDate = new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

    // Awkward-to-round amounts that would expose any change in the money path.
    private const decimal IncomeAmount = 1234.55m;
    private const decimal ExpenseAmount = 987.05m;

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

        // DR Cash / CR Income, and DR Expense / CR Cash — balanced pairs across accounts.
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = IncomeAmount, CreditAmount = 0m, Description = "Dues receipt", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = IncomeId, GLAccount = "4000", DebitAmount = 0m, CreditAmount = IncomeAmount, Description = "Dues income", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = ExpenseId, GLAccount = "6000", DebitAmount = ExpenseAmount, CreditAmount = 0m, Description = "Hall hire", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = 0m, CreditAmount = ExpenseAmount, Description = "Hall hire payment", CreatedAt = DateTime.UtcNow });

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1,
            CurrencyCode = "AUD", IsTaxApplicable = false,
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

    /// <summary>The legacy (pre-028) rendering of an AUD amount: the plain number with a "$".</summary>
    private static string LegacyAud(decimal amount) =>
        (amount < 0 ? "-$" : "$") + Math.Abs(amount).ToString("N2", CultureInfo.CurrentCulture);

    [Fact]
    public void MoneyFormatter_ForAud_IsByteIdenticalToTheLegacyDollarString()
    {
        foreach (var amount in new[] { 0m, 5m, IncomeAmount, ExpenseAmount, 1_234_567.89m, -42.10m })
            Assert.Equal(LegacyAud(amount), MoneyFormatter.Format(amount));

        Assert.Equal("AUD " + 1234.55m.ToString("N2", CultureInfo.CurrentCulture), MoneyFormatter.FormatWithCode(1234.55m));
    }

    [Fact]
    public void TaxCalculator_AtTwoMinorDigits_IsUnchanged()
    {
        var (net, tax) = TaxCalculator.SplitInclusive(110m, 10m);
        Assert.Equal(10m, tax);
        Assert.Equal(100m, net);
        Assert.Equal(110m, net + tax);
    }

    [Fact]
    public async Task IncomeStatement_FiguresAreTheSameNumbers_NowWithADollarSign()
    {
        var result = await BuildIncome().GenerateAsync(FyFilters(), TestContext.Current.CancellationToken);

        var incomeRow = result.Sections.First(s => s.Heading == "Income").Rows.Single(r => r.Cells[0] == "Membership Dues");
        var expenseRow = result.Sections.First(s => s.Heading == "Expenses").Rows.Single(r => r.Cells[0] == "Hall Hire");

        Assert.Equal(LegacyAud(IncomeAmount), incomeRow.Cells[1]);
        Assert.Equal(LegacyAud(ExpenseAmount), expenseRow.Cells[1]);
        Assert.Equal(LegacyAud(IncomeAmount - ExpenseAmount), result.GrandTotal!.Cells[1]);
    }

    [Fact]
    public async Task TrialBalance_TiesAndShowsTheSameTotals()
    {
        var result = await BuildTrialBalance().GenerateAsync(FyFilters(), TestContext.Current.CancellationToken);

        // debits == credits, so it generates (no GLBalanceException) and the totals row is the legacy number.
        Assert.Equal(LegacyAud(IncomeAmount + ExpenseAmount), result.GrandTotal!.Cells[1]);
        Assert.Equal(LegacyAud(IncomeAmount + ExpenseAmount), result.GrandTotal.Cells[2]);
    }

    [Fact]
    public async Task StoredTransactionValues_AreUntouched()
    {
        var stored = await _db.Transactions.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, stored.Count);
        Assert.Equal(2, stored.Count(t => t.CreditAmount == IncomeAmount || t.DebitAmount == IncomeAmount));
        Assert.Equal(2, stored.Count(t => t.CreditAmount == ExpenseAmount || t.DebitAmount == ExpenseAmount));
        Assert.Equal(IncomeAmount + ExpenseAmount, stored.Sum(t => t.DebitAmount));
        Assert.Equal(IncomeAmount + ExpenseAmount, stored.Sum(t => t.CreditAmount));
    }

    private IncomeStatementReportProvider BuildIncome() =>
        new(new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private TrialBalanceReportProvider BuildTrialBalance() =>
        new(new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private static ReportFilterValues FyFilters()
    {
        var f = new ReportFilterValues();
        f.Set("period", "Custom");   // Income Statement honours dateFrom/dateTo only for a Custom period
        f.Set("dateFrom", "2026-03-01");
        f.Set("dateTo", "2026-03-31");
        return f;
    }
}
