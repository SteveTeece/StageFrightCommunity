using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for BalanceSheetReportProvider:
/// - Assets / Liabilities / Equity sections with correct debit/credit-normal signs
/// - Computed Accumulated Surplus row (net income since inception)
/// - Assets = Liabilities + Equity identity on a fixture ledger
/// - Archived zero-balance accounts omitted; the seeded Accumulated Surplus account
///   never contributes its own (always-zero) GL balance
/// - As-at filter parsed and passed through to GetAccountBalanceAsync
/// </summary>
public class BalanceSheetReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ISettingsRepository _settings = Substitute.For<ISettingsRepository>();
    private readonly BalanceSheetReportProvider _sut;

    public BalanceSheetReportProviderTests()
    {
        _sut = new BalanceSheetReportProvider(_gl, _accounts, _settings, RealLocalizer.Instance);
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
    }

    [Fact]
    public void ReportId_IsBalanceSheet()
    {
        Assert.Equal("balance-sheet", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public void Filters_HasAsAtDate_DefaultingToFyEnd()
    {
        var (_, fyEnd) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
        var filter = _sut.Filters.FirstOrDefault(f => f.Key == "asAt");

        Assert.NotNull(filter);
        Assert.Equal($"{fyEnd:yyyy-MM-dd}", filter!.DefaultValue);
    }

    [Fact]
    public async Task GenerateAsync_AssetAccount_DisplaysDebitNormalBalance()
    {
        var cashId = Guid.NewGuid();
        SetupAccounts(MakeAccount(cashId, "Cash on Hand", AccountType.Asset, "1100"));
        SetupBalance(cashId, 1000m);

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var assets = result.Sections.First(s => s.Heading == "Assets");
        Assert.Contains(assets.Rows, r => r.Cells[0].Contains("Cash on Hand") && r.Cells[1] == MoneyFormatter.Format(1000m));
    }

    [Fact]
    public async Task GenerateAsync_LiabilityAccount_DisplaysCreditNormalBalance()
    {
        var liabId = Guid.NewGuid();
        SetupAccounts(MakeAccount(liabId, "GST Collected", AccountType.Liability, "2310"));
        SetupBalance(liabId, -200m); // net credit position

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var liabilities = result.Sections.First(s => s.Heading == "Liabilities");
        Assert.Contains(liabilities.Rows, r => r.Cells[1] == MoneyFormatter.Format(200m));
        Assert.Equal(MoneyFormatter.Format(200m), liabilities.Subtotal!.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_Equity_IncludesComputedAccumulatedSurplusRow()
    {
        var incomeId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        SetupAccounts(
            MakeAccount(incomeId, "Dues", AccountType.Income, "4000"),
            MakeAccount(expenseId, "Hall Hire", AccountType.Expense, "6000"));
        SetupBalance(incomeId, -500m); // $500 net credit = $500 income
        SetupBalance(expenseId, 300m); // $300 net debit = $300 expense

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var equity = result.Sections.First(s => s.Heading == "Equity");
        Assert.Contains(equity.Rows, r => r.Cells[0] == "Accumulated Surplus" && r.Cells[1] == MoneyFormatter.Format(200m));
    }

    [Fact]
    public async Task GenerateAsync_SeededAccumulatedSurplusAccount_DoesNotDuplicateAsGLRow()
    {
        SetupAccounts(MakeAccount(SystemAccounts.AccumulatedSurplusId, "Accumulated Surplus", AccountType.Equity, SystemAccounts.AccumulatedSurplusNumber, isSystem: true));
        SetupBalance(SystemAccounts.AccumulatedSurplusId, 0m);

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var equity = result.Sections.First(s => s.Heading == "Equity");
        Assert.Single(equity.Rows, r => r.Cells[0] == "Accumulated Surplus");
    }

    [Fact]
    public async Task GenerateAsync_ArchivedZeroBalanceAccount_Omitted()
    {
        var archivedId = Guid.NewGuid();
        var account = MakeAccount(archivedId, "Old Petty Cash", AccountType.Asset, "1150");
        account.IsDeleted = true;
        SetupAccounts();
        _accounts.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>([account]));
        SetupBalance(archivedId, 0m);

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var assets = result.Sections.First(s => s.Heading == "Assets");
        Assert.DoesNotContain(assets.Rows, r => r.Cells[0].Contains("Old Petty Cash"));
    }

    [Fact]
    public async Task GenerateAsync_AssetsEqualsLiabilitiesPlusEquity()
    {
        var cashId = Guid.NewGuid();
        var liabId = Guid.NewGuid();
        var equityId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        SetupAccounts(
            MakeAccount(cashId, "Cash", AccountType.Asset, "1100"),
            MakeAccount(liabId, "GST Collected", AccountType.Liability, "2310"),
            MakeAccount(equityId, "Opening Balance Equity", AccountType.Equity, "3100"),
            MakeAccount(incomeId, "Dues", AccountType.Income, "4000"),
            MakeAccount(expenseId, "Hall Hire", AccountType.Expense, "6000"));

        SetupBalance(cashId, 1000m);
        SetupBalance(liabId, -200m);
        SetupBalance(equityId, -600m);
        SetupBalance(incomeId, -500m);
        SetupBalance(expenseId, 300m);

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var totalAssets = result.Sections.First(s => s.Heading == "Assets").Subtotal!.Cells[1];
        var totalLiabPlusEquity = result.GrandTotal!.Cells[1];

        Assert.Equal(MoneyFormatter.Format(1000m), totalAssets);
        Assert.Equal(MoneyFormatter.Format(1000m), totalLiabPlusEquity);
    }

    [Fact]
    public async Task GenerateAsync_AsAtFilter_Applied()
    {
        var cashId = Guid.NewGuid();
        SetupAccounts(MakeAccount(cashId, "Cash", AccountType.Asset, "1100"));
        SetupBalance(cashId, 100m);

        var filters = new ReportFilterValues();
        filters.Set("asAt", "2026-06-30");

        await _sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        await _gl.Received().GetAccountBalanceAsync(
            cashId,
            new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WhenLedgerBalances_AppendsNoOutOfBalanceRow()
    {
        var cashId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        SetupAccounts(
            MakeAccount(cashId, "Cash", AccountType.Asset, "1100"),
            MakeAccount(incomeId, "Dues", AccountType.Income, "4000"));
        SetupBalance(cashId, 500m);    // asset net debit 500
        SetupBalance(incomeId, -500m); // income net credit 500 → accumulated surplus 500

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var outOfBalanceLabel = RealLocalizer.Instance.Get<ReportsResource>("Reports_BalanceSheet_OutOfBalance");
        Assert.DoesNotContain(
            result.Sections.SelectMany(s => s.Rows),
            r => r.Cells.Count > 0 && r.Cells[0] == outOfBalanceLabel);
        Assert.Equal(3, result.Sections.Count);
    }

    [Fact]
    public async Task GenerateAsync_WhenAssetsDoNotEqualLiabilitiesPlusEquity_AppendsExplicitOutOfBalanceRow()
    {
        var cashId = Guid.NewGuid();
        SetupAccounts(MakeAccount(cashId, "Cash", AccountType.Asset, "1100"));
        SetupBalance(cashId, 1000m); // assets 1000, nothing else → liabilities + equity 0

        var result = await _sut.GenerateAsync(AsAtFilters(), TestContext.Current.CancellationToken);

        var outOfBalanceLabel = RealLocalizer.Instance.Get<ReportsResource>("Reports_BalanceSheet_OutOfBalance");
        var flagged = result.Sections.SelectMany(s => s.Rows)
            .SingleOrDefault(r => r.Cells.Count > 0 && r.Cells[0] == outOfBalanceLabel);

        Assert.NotNull(flagged);
        Assert.True(flagged!.IsEmphasized);
        Assert.Equal(MoneyFormatter.Format(1000m), flagged.Cells[1]);

        // Never a clean statement: total assets and total liabilities + equity visibly disagree.
        var totalAssets = result.Sections.First(s => s.Heading == "Assets").Subtotal!.Cells[1];
        Assert.NotEqual(totalAssets, result.GrandTotal!.Cells[1]);
    }

    // --- Helpers ---

    private void SetupAccounts(params Account[] accounts)
    {
        _accounts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>(accounts.ToList()));
        _accounts.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>(Array.Empty<Account>()));
    }

    private void SetupBalance(Guid accountId, decimal netDebitBalance)
    {
        _gl.GetAccountBalanceAsync(accountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(netDebitBalance);
    }

    private static Account MakeAccount(Guid id, string name, AccountType type, string number, bool isSystem = false)
        => new() { Id = id, Name = name, Type = type, AccountNumber = number, IsSystem = isSystem, CreatedAt = DateTime.UtcNow };

    private static ReportFilterValues AsAtFilters()
    {
        var f = new ReportFilterValues();
        f.Set("asAt", "2026-06-30");
        return f;
    }
}
