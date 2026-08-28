using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Rendering;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for ChartOfAccountsReportProvider:
/// - Identity (ReportId/ReportName/ModuleName/DisplayOrder)
/// - Five fixed sections in fixed order, empty sections still appear
/// - Rows ordered by AccountNumber ascending
/// - System/Bank/System+Bank Name-suffix formatting
/// - Structural two-column shape, GrandTotal/SummaryColumns always null
/// - Archived accounts never read (GetArchivedAccountBalancesAsync never called)
/// </summary>
public class ChartOfAccountsReportProviderTests
{
    private readonly IAccountBalanceService _balanceService = Substitute.For<IAccountBalanceService>();
    private readonly ChartOfAccountsReportProvider _sut;

    public ChartOfAccountsReportProviderTests()
    {
        _sut = new ChartOfAccountsReportProvider(_balanceService, RealLocalizer.Instance);
    }

    [Fact]
    public void ReportId_IsChartOfAccounts()
    {
        Assert.Equal("chart-of-accounts", _sut.ReportId);
    }

    [Fact]
    public void ReportName_IsChartOfAccounts()
    {
        Assert.Equal("Chart of Accounts", _sut.ReportName);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public void DisplayOrder_Is15()
    {
        Assert.Equal(15, _sut.DisplayOrder);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsExactlyFiveSections_InFixedOrder()
    {
        SetupBalances();

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Sections.Count);
        Assert.Equal(["Assets", "Liabilities", "Equity", "Income", "Expenses"],
            result.Sections.Select(s => s.Heading!).ToArray());
    }

    [Fact]
    public async Task GenerateAsync_TypeWithNoAccounts_StillAppearsWithZeroRows()
    {
        // Only an Asset account exists — the other four sections must still appear, empty.
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        var liabilities = result.Sections.Single(s => s.Heading == "Liabilities");
        Assert.Empty(liabilities.Rows);
    }

    [Fact]
    public async Task GenerateAsync_RowsWithinSection_OrderedByAccountNumberAscending()
    {
        SetupBalances(
            MakeBalance("1200", "Second Asset", AccountType.Asset),
            MakeBalance("1100", "First Asset", AccountType.Asset));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        var assets = result.Sections.Single(s => s.Heading == "Assets");
        Assert.Equal(["1100", "1200"], assets.Rows.Select(r => r.Cells[0]).ToArray());
    }

    [Fact]
    public async Task GenerateAsync_SystemAccount_NameHasSystemSuffix()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset, isSystem: true));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single(s => s.Heading == "Assets").Rows.Single();
        Assert.Equal("Cash on Hand (System)", row.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_BankAccount_NameHasBankSuffix()
    {
        SetupBalances(MakeBalance("1110", "Operating Account", AccountType.Asset, isBankAccount: true));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single(s => s.Heading == "Assets").Rows.Single();
        Assert.Equal("Operating Account (Bank)", row.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_SystemAndBankAccount_NameHasCombinedSuffix()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset, isSystem: true, isBankAccount: true));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single(s => s.Heading == "Assets").Rows.Single();
        Assert.Equal("Cash on Hand (System, Bank)", row.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_PlainAccount_NameHasNoSuffix()
    {
        SetupBalances(MakeBalance("4000", "Membership Fees", AccountType.Income));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single(s => s.Heading == "Income").Rows.Single();
        Assert.Equal("Membership Fees", row.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_ColumnsAreExactlyNumberAndName()
    {
        SetupBalances();

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        Assert.Equal(["No.", "Name"], result.Columns.Select(c => c.Header).ToArray());
    }

    [Fact]
    public async Task GenerateAsync_EveryRow_HasTwoCells()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single(s => s.Heading == "Assets").Rows.Single();
        Assert.Equal(2, row.Cells.Count);
    }

    [Fact]
    public async Task GenerateAsync_GrandTotal_IsAlwaysNull()
    {
        SetupBalances();

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        Assert.Null(result.GrandTotal);
    }

    [Fact]
    public async Task GenerateAsync_SummaryColumns_IsAlwaysNull()
    {
        SetupBalances();

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        Assert.Null(result.SummaryColumns);
    }

    [Fact]
    public async Task GenerateAsync_NeverCallsGetArchivedAccountBalancesAsync()
    {
        SetupBalances();

        await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        await _balanceService.DidNotReceive().GetArchivedAccountBalancesAsync(Arg.Any<CancellationToken>());
    }

    // --- includeBalances filter ---

    [Fact]
    public void Filters_ReturnsExactlyOneIncludeBalancesDefinition()
    {
        var filter = Assert.Single(_sut.Filters);

        Assert.Equal("includeBalances", filter.Key);
        Assert.Equal(ReportFilterType.Boolean, filter.Type);
        Assert.Equal("Include Current Balances", filter.Label);
        Assert.Equal("false", filter.DefaultValue);
    }

    [Fact]
    public async Task GenerateAsync_IncludeBalancesUnset_TwoColumnShapeUnchanged()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset));

        var result = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);

        Assert.Equal(["No.", "Name"], result.Columns.Select(c => c.Header).ToArray());
        var row = result.Sections.Single(s => s.Heading == "Assets").Rows.Single();
        Assert.Equal(2, row.Cells.Count);
    }

    [Fact]
    public async Task GenerateAsync_IncludeBalancesFalse_TwoColumnShapeUnchanged()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset));

        var result = await _sut.GenerateAsync(FiltersWith("false"), TestContext.Current.CancellationToken);

        Assert.Equal(["No.", "Name"], result.Columns.Select(c => c.Header).ToArray());
        var row = result.Sections.Single(s => s.Heading == "Assets").Rows.Single();
        Assert.Equal(2, row.Cells.Count);
    }

    [Fact]
    public async Task GenerateAsync_IncludeBalancesTrue_AddsBalanceColumn()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset));

        var result = await _sut.GenerateAsync(FiltersWith("true"), TestContext.Current.CancellationToken);

        Assert.Equal(["No.", "Name", "Balance"], result.Columns.Select(c => c.Header).ToArray());
    }

    [Fact]
    public async Task GenerateAsync_IncludeBalancesTrue_NoError_ShowsFormattedBalance()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset, balance: 500m));

        var result = await _sut.GenerateAsync(FiltersWith("true"), TestContext.Current.CancellationToken);

        var row = result.Sections.Single(s => s.Heading == "Assets").Rows.Single();
        Assert.Equal(3, row.Cells.Count);
        Assert.Equal("500.00", row.Cells[2]);
    }

    [Fact]
    public async Task GenerateAsync_IncludeBalancesTrue_HasError_ShowsErrorText()
    {
        SetupBalances(
            MakeBalance("1100", "Cash on Hand", AccountType.Asset, balance: 500m),
            MakeBalance("1200", "Broken Asset", AccountType.Asset, balance: null, hasError: true));

        var result = await _sut.GenerateAsync(FiltersWith("true"), TestContext.Current.CancellationToken);

        var assets = result.Sections.Single(s => s.Heading == "Assets").Rows;
        Assert.Equal("500.00", assets.Single(r => r.Cells[0] == "1100").Cells[2]);
        Assert.Equal("Error", assets.Single(r => r.Cells[0] == "1200").Cells[2]);
    }

    // --- CSV export round-trip (SC-006) ---

    [Fact]
    public async Task GenerateAsync_ExportedViaCsv_IncludeBalancesFalse_HeaderAndDataMatchOutput()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset));

        var report = await _sut.GenerateAsync(NoFilters(), TestContext.Current.CancellationToken);
        var csv = new CsvReportExporter().Export(report);
        var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        Assert.Equal("No.,Name", lines[0]);
        Assert.Contains(lines, l => l == "1100,Cash on Hand");
    }

    [Fact]
    public async Task GenerateAsync_ExportedViaCsv_IncludeBalancesTrue_HeaderAndDataMatchOutput()
    {
        SetupBalances(MakeBalance("1100", "Cash on Hand", AccountType.Asset, balance: 500m));

        var report = await _sut.GenerateAsync(FiltersWith("true"), TestContext.Current.CancellationToken);
        var csv = new CsvReportExporter().Export(report);
        var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        Assert.Equal("No.,Name,Balance", lines[0]);
        Assert.Contains(lines, l => l == "1100,Cash on Hand,500.00");
    }

    // --- Helpers ---

    private void SetupBalances(params AccountBalance[] balances)
    {
        _balanceService.GetActiveAccountBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AccountBalance>>(balances.ToList()));
    }

    private static AccountBalance MakeBalance(
        string accountNumber, string name, AccountType type,
        bool isSystem = false, bool isBankAccount = false, decimal? balance = 0m, bool hasError = false)
        => new()
        {
            AccountId = Guid.NewGuid(),
            AccountNumber = accountNumber,
            Name = name,
            Type = type,
            IsSystem = isSystem,
            IsBankAccount = isBankAccount,
            Balance = balance,
            HasError = hasError
        };

    private static ReportFilterValues NoFilters() => new();

    private static ReportFilterValues FiltersWith(string includeBalances)
    {
        var f = new ReportFilterValues();
        f.Set("includeBalances", includeBalances);
        return f;
    }
}
