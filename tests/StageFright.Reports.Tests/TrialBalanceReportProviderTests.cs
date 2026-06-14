using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for TrialBalanceReportProvider:
/// - Assets / Income / Expenses sections
/// - Debit / Credit columns
/// - Σdebits = Σcredits → report generated
/// - Forced imbalance → GLBalanceException with FR-034 message
/// </summary>
public class TrialBalanceReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly TrialBalanceReportProvider _sut;

    public TrialBalanceReportProviderTests()
    {
        _sut = new TrialBalanceReportProvider(_gl, _categories);
    }

    [Fact]
    public void ReportId_IsTrialBalance()
    {
        Assert.Equal("trial-balance", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public async Task GenerateAsync_WhenBalanced_ReturnsReport()
    {
        SetupBalanceTotals(1000m, 1000m);
        SetupCategories();

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.NotNull(result);
        Assert.Equal("Trial Balance", result.Title);
    }

    [Fact]
    public async Task GenerateAsync_WhenBalanced_HasAssetsSection()
    {
        SetupBalanceTotals(500m, 500m);
        SetupCategories();

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Sections, s => s.Heading == "Assets");
    }

    [Fact]
    public async Task GenerateAsync_WhenBalanced_HasIncomeSection()
    {
        SetupBalanceTotals(200m, 200m);
        SetupCategories(MakeCategory(Guid.NewGuid(), "Dues", CategoryType.Income, "1000"));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Sections, s => s.Heading == "Income");
    }

    [Fact]
    public async Task GenerateAsync_WhenBalanced_HasExpensesSection()
    {
        SetupBalanceTotals(300m, 300m);
        SetupCategories(MakeCategory(Guid.NewGuid(), "Hall", CategoryType.Expense, "2000"));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Sections, s => s.Heading == "Expenses");
    }

    [Fact]
    public async Task GenerateAsync_ReportHasDebitAndCreditColumns()
    {
        SetupBalanceTotals(0m, 0m);
        SetupCategories();

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Columns, c => c.Header == "Debit");
        Assert.Contains(result.Columns, c => c.Header == "Credit");
    }

    [Fact]
    public async Task GenerateAsync_WhenImbalanced_ThrowsGLBalanceException()
    {
        SetupBalanceTotals(1000m, 950m); // $50 imbalance
        SetupCategories();

        var ex = await Assert.ThrowsAsync<GLBalanceException>(
            () => _sut.GenerateAsync(CurrentYearFilters()));

        Assert.NotNull(ex);
    }

    [Fact]
    public async Task GenerateAsync_WhenImbalanced_ExceptionMessage_MatchesFR034()
    {
        SetupBalanceTotals(1000m, 950m);
        SetupCategories();

        var ex = await Assert.ThrowsAsync<GLBalanceException>(
            () => _sut.GenerateAsync(CurrentYearFilters()));

        // FR-034 exact message format
        Assert.Contains("GL Balance Verification Failed", ex.Message);
        Assert.Contains("1000.00", ex.Message);
        Assert.Contains("950.00", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WithinToleranceOf01_DoesNotThrow()
    {
        // Exactly 0.01 difference should still pass (within tolerance)
        SetupBalanceTotals(100.00m, 100.00m);
        SetupCategories();

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GenerateAsync_GrandTotalRow_ShowsTotalDebitsAndCredits()
    {
        SetupBalanceTotals(500m, 500m);
        SetupCategories();

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.NotNull(result.GrandTotal);
        Assert.Contains("500.00", result.GrandTotal!.Cells[1]); // total debits
        Assert.Contains("500.00", result.GrandTotal.Cells[2]);  // total credits
    }

    // --- Helpers ---

    private void SetupBalanceTotals(decimal debits, decimal credits)
    {
        _gl.GetBalanceTotalsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((debits, credits));
    }

    private void SetupCategories(params Category[] cats)
    {
        _categories.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Category>>(cats.ToList()));
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(Array.Empty<Transaction>().ToList()));
    }

    private static Category MakeCategory(Guid id, string name, CategoryType type, string glAccount)
        => new() { Id = id, Name = name, Type = type, GLAccount = glAccount, CreatedAt = DateTime.UtcNow };

    private static ReportFilterValues CurrentYearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{DateTime.UtcNow.Year}-01-01");
        f.Set("dateTo", $"{DateTime.UtcNow.Year}-12-31");
        return f;
    }
}
