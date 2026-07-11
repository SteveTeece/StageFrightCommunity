using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for IncomeStatementReportProvider ("Statement of Income & Expenditure"):
/// - Income / Expense sections with subtotal
/// - Surplus / (Deficit) grand total
/// - FY presets (This FY / Last FY / Custom) resolve to the expected range
/// - Prior-year comparison column, only rendered when requested
/// - Empty sections handled without throwing
/// </summary>
public class IncomeStatementReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ISettingsRepository _settings = Substitute.For<ISettingsRepository>();
    private readonly IncomeStatementReportProvider _sut;

    public IncomeStatementReportProviderTests()
    {
        _sut = new IncomeStatementReportProvider(_gl, _accounts, _settings);
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        SetupAccounts();
        SetupMovements();
    }

    [Fact]
    public void ReportId_IsIncomeStatement()
    {
        Assert.Equal("income-statement", _sut.ReportId);
    }

    [Fact]
    public void ReportName_IsStatementOfIncomeAndExpenditure()
    {
        Assert.Equal("Statement of Income & Expenditure", _sut.ReportName);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public void Filters_HasPeriodPreset_DefaultingToThisFy()
    {
        var filter = _sut.Filters.FirstOrDefault(f => f.Key == "period");

        Assert.NotNull(filter);
        Assert.Equal(ReportFilterType.Select, filter!.Type);
        Assert.Equal(["This FY", "Last FY", "Custom"], filter.Options);
        Assert.Equal("This FY", filter.DefaultValue);
    }

    [Fact]
    public void Filters_HasCompareCheckbox_DefaultingToFalse()
    {
        var filter = _sut.Filters.FirstOrDefault(f => f.Key == "compare");

        Assert.NotNull(filter);
        Assert.Equal(ReportFilterType.Boolean, filter!.Type);
        Assert.Equal("false", filter.DefaultValue);
    }

    [Fact]
    public async Task GenerateAsync_IncomeSection_ContainsIncomeAccountRow()
    {
        var catId = Guid.NewGuid();
        SetupAccounts(MakeAccount(catId, "Membership Dues", AccountType.Income, "4000"));
        SetupMovements((catId, 0m, 100m));

        var result = await _sut.GenerateAsync(PeriodFilters("This FY"));

        var incomeSection = result.Sections.First(s => s.Heading == "Income");
        Assert.Contains(incomeSection.Rows, r => r.Cells[0].Contains("Membership Dues"));
    }

    [Fact]
    public async Task GenerateAsync_IncomeSection_SubtotalEqualsSum()
    {
        var catId = Guid.NewGuid();
        SetupAccounts(MakeAccount(catId, "Dues", AccountType.Income, "4000"));
        SetupMovements((catId, 0m, 150m));

        var result = await _sut.GenerateAsync(PeriodFilters("This FY"));

        var incomeSection = result.Sections.First(s => s.Heading == "Income");
        Assert.Equal("150.00", incomeSection.Subtotal!.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_ExpenseSection_ContainsExpenseAccountRow()
    {
        var catId = Guid.NewGuid();
        SetupAccounts(MakeAccount(catId, "Hall Hire", AccountType.Expense, "6000"));
        SetupMovements((catId, 200m, 0m));

        var result = await _sut.GenerateAsync(PeriodFilters("This FY"));

        var expenseSection = result.Sections.First(s => s.Heading == "Expenses");
        Assert.Contains(expenseSection.Rows, r => r.Cells[0].Contains("Hall Hire"));
    }

    [Fact]
    public async Task GenerateAsync_GrandTotal_IsSurplus_WhenIncomeExceedsExpense()
    {
        var incId = Guid.NewGuid();
        var expId = Guid.NewGuid();
        SetupAccounts(
            MakeAccount(incId, "Dues", AccountType.Income, "4000"),
            MakeAccount(expId, "Hall", AccountType.Expense, "6000"));
        SetupMovements((incId, 0m, 300m), (expId, 100m, 0m));

        var result = await _sut.GenerateAsync(PeriodFilters("This FY"));

        Assert.Equal("Surplus", result.GrandTotal!.Cells[0]);
        Assert.Equal("200.00", result.GrandTotal.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_GrandTotal_IsDeficit_WhenExpenseExceedsIncome()
    {
        var incId = Guid.NewGuid();
        var expId = Guid.NewGuid();
        SetupAccounts(
            MakeAccount(incId, "Dues", AccountType.Income, "4000"),
            MakeAccount(expId, "Hall", AccountType.Expense, "6000"));
        SetupMovements((incId, 0m, 50m), (expId, 200m, 0m));

        var result = await _sut.GenerateAsync(PeriodFilters("This FY"));

        Assert.Equal("(Deficit)", result.GrandTotal!.Cells[0]);
        Assert.Equal("-150.00", result.GrandTotal.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_EmptySections_HandledWithoutThrowing()
    {
        var result = await _sut.GenerateAsync(PeriodFilters("This FY"));

        Assert.NotNull(result);
        Assert.Equal("Statement of Income & Expenditure", result.Title);
    }

    [Fact]
    public async Task GenerateAsync_ThisFy_UsesFinancialYearRange()
    {
        var (from, to) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);

        await _sut.GenerateAsync(PeriodFilters("This FY"));

        await _gl.Received().GetAccountMovementsAsync(from, to, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_LastFy_UsesPreviousFinancialYearRange()
    {
        var (from, to) = FinancialYearCalculator.GetPreviousRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);

        await _sut.GenerateAsync(PeriodFilters("Last FY"));

        await _gl.Received().GetAccountMovementsAsync(from, to, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_Custom_UsesProvidedDateRange()
    {
        var filters = PeriodFilters("Custom");
        filters.Set("dateFrom", "2026-01-01");
        filters.Set("dateTo", "2026-06-30");

        await _sut.GenerateAsync(filters);

        await _gl.Received().GetAccountMovementsAsync(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_CompareOff_ColumnsAreAccountAndAmount()
    {
        var result = await _sut.GenerateAsync(PeriodFilters("This FY"));

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal("Amount", result.Columns[1].Header);
    }

    [Fact]
    public async Task GenerateAsync_CompareOn_AddsPriorPeriodColumnAndValues()
    {
        var incId = Guid.NewGuid();
        SetupAccounts(MakeAccount(incId, "Dues", AccountType.Income, "4000"));

        var (from, to) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FinancialYearCalculator.DefaultStartMonth);
        _gl.GetAccountMovementsAsync(from, to, Arg.Any<CancellationToken>())
            .Returns(Movements((incId, 0m, 300m)));
        _gl.GetAccountMovementsAsync(from.AddYears(-1), to.AddYears(-1), Arg.Any<CancellationToken>())
            .Returns(Movements((incId, 0m, 100m)));

        var filters = PeriodFilters("This FY");
        filters.Set("compare", "true");

        var result = await _sut.GenerateAsync(filters);

        Assert.Equal(3, result.Columns.Count);
        Assert.Equal("Prior Period", result.Columns[2].Header);
        var incomeRow = result.Sections.First(s => s.Heading == "Income").Rows.First(r => r.Cells[0] == "Dues");
        Assert.Equal("300.00", incomeRow.Cells[1]);
        Assert.Equal("100.00", incomeRow.Cells[2]);
    }

    // --- Helpers ---

    private void SetupAccounts(params Account[] accounts)
    {
        _accounts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>(accounts.ToList()));
        _accounts.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>(Array.Empty<Account>()));
    }

    private void SetupMovements(params (Guid AccountId, decimal Debits, decimal Credits)[] movements)
    {
        _gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Movements(movements));
    }

    private static IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)> Movements(
        params (Guid AccountId, decimal Debits, decimal Credits)[] movements)
        => movements.ToDictionary(m => m.AccountId, m => (m.Debits, m.Credits));

    private static Account MakeAccount(Guid id, string name, AccountType type, string number)
        => new() { Id = id, Name = name, Type = type, AccountNumber = number, CreatedAt = DateTime.UtcNow };

    private static ReportFilterValues PeriodFilters(string period)
    {
        var f = new ReportFilterValues();
        f.Set("period", period);
        return f;
    }
}
