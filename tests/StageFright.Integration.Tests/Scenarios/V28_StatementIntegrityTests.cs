using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Resources;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// V28 acceptance — US3 AC-1…AC-3 (spec 028): the Balance Sheet and Trial Balance from a balanced
/// ledger both tie and produce clean statements; from a deliberately corrupted ledger the Balance
/// Sheet shows an explicit out-of-balance line and never a clean statement and the Trial Balance
/// fails to generate, with a one-cent debit/credit difference still failing (no tolerance band).
/// Real in-memory SQLite + full migrations.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class V28_StatementIntegrityTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly DateTime PostDate = new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

    private const decimal IncomeAmount = 1_000m;
    private const decimal ExpenseAmount = 400m;

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

        // Balanced pairs: DR Cash / CR Income, and DR Expense / CR Cash.
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = IncomeAmount, CreditAmount = 0m, Description = "Dues receipt", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = IncomeId, GLAccount = "4000", DebitAmount = 0m, CreditAmount = IncomeAmount, Description = "Dues income", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = ExpenseId, GLAccount = "6000", DebitAmount = ExpenseAmount, CreditAmount = 0m, Description = "Hall hire", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = 0m, CreditAmount = ExpenseAmount, Description = "Hall hire payment", CreatedAt = DateTime.UtcNow });

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Integrity Players",
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

    // AC-1: a balanced ledger — both statements tie and are produced normally.
    [Fact]
    public async Task BalancedLedger_TrialBalance_TiesAndGenerates()
    {
        var trial = await BuildTrialBalance().GenerateAsync(RangeFilters(), TestContext.Current.CancellationToken);

        Assert.Equal(trial.GrandTotal!.Cells[1], trial.GrandTotal.Cells[2]);
        Assert.Equal(MoneyFormatter.Format(IncomeAmount + ExpenseAmount), trial.GrandTotal.Cells[1]);
    }

    [Fact]
    public async Task BalancedLedger_BalanceSheet_ProducesACleanStatement()
    {
        var balance = await BuildBalanceSheet().GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var totalAssets = balance.Sections.First(s => s.Heading == "Assets").Subtotal!.Cells[1];
        Assert.Equal(totalAssets, balance.GrandTotal!.Cells[1]);
        Assert.Equal(3, balance.Sections.Count);
        Assert.DoesNotContain(
            balance.Sections.SelectMany(s => s.Rows),
            r => r.Cells.Count > 0 && r.Cells[0] == OutOfBalanceLabel());
    }

    // AC-2: a corrupted ledger — the Balance Sheet flags the imbalance and is never clean.
    [Fact]
    public async Task CorruptedLedger_BalanceSheet_ShowsExplicitOutOfBalanceLine_AndNeverACleanStatement()
    {
        await CorruptLedgerAsync(100m);

        var balance = await BuildBalanceSheet().GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var flagged = balance.Sections.SelectMany(s => s.Rows)
            .SingleOrDefault(r => r.Cells.Count > 0 && r.Cells[0] == OutOfBalanceLabel());
        Assert.NotNull(flagged);
        Assert.True(flagged!.IsEmphasized);
        Assert.Equal(MoneyFormatter.Format(100m), flagged.Cells[1]);

        var totalAssets = balance.Sections.First(s => s.Heading == "Assets").Subtotal!.Cells[1];
        Assert.NotEqual(totalAssets, balance.GrandTotal!.Cells[1]);
    }

    // AC-3: a corrupted ledger — the Trial Balance refuses to generate.
    [Fact]
    public async Task CorruptedLedger_TrialBalance_FailsToGenerate()
    {
        await CorruptLedgerAsync(100m);

        await Assert.ThrowsAsync<GLBalanceException>(
            () => BuildTrialBalance().GenerateAsync(RangeFilters(), TestContext.Current.CancellationToken));
    }

    // AC-3: a one-cent difference still fails — there is no tolerance band.
    [Fact]
    public async Task TrialBalance_WithAOneCentImbalance_StillFailsToGenerate()
    {
        await CorruptLedgerAsync(0.01m);

        await Assert.ThrowsAsync<GLBalanceException>(
            () => BuildTrialBalance().GenerateAsync(RangeFilters(), TestContext.Current.CancellationToken));
    }

    private async Task CorruptLedgerAsync(decimal danglingDebit)
    {
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            Date = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            AccountId = SystemAccounts.CashId, GLAccount = "1100",
            DebitAmount = danglingDebit, CreditAmount = 0m,
            Description = "Corrupted entry — no matching credit", CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static string OutOfBalanceLabel() =>
        RealLocalizer.Instance.Get<ReportsResource>("Reports_BalanceSheet_OutOfBalance");

    private TrialBalanceReportProvider BuildTrialBalance() =>
        new(new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private BalanceSheetReportProvider BuildBalanceSheet() =>
        new(new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new AccountRepository(_db), new SettingsRepository(_db), RealLocalizer.Instance);

    private static ReportFilterValues RangeFilters()
    {
        var f = new ReportFilterValues();
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
