using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for IncomeStatementReportProvider:
/// - Income section populated with subtotal
/// - Expense section populated with subtotal
/// - Net income / net loss grand total
/// - Date-range filter applied
/// - Empty income/expense sections handled without throwing
/// </summary>
public class IncomeStatementReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IncomeStatementReportProvider _sut;

    public IncomeStatementReportProviderTests()
    {
        _sut = new IncomeStatementReportProvider(_gl, _categories);
    }

    [Fact]
    public void ReportId_IsIncomeStatement()
    {
        Assert.Equal("income-statement", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public void HasDateRangeFilter_DefaultingToCurrentYear()
    {
        var filter = _sut.Filters.FirstOrDefault(f => f.Key == "dateFrom");
        Assert.NotNull(filter);
        Assert.Equal(DateTime.UtcNow.Year.ToString(), filter!.DefaultValue?.Substring(0, 4));
    }

    [Fact]
    public async Task GenerateAsync_IncomeSection_ContainsIncomeCategoryRow()
    {
        var catId = Guid.NewGuid();
        SetupCategories(
            MakeCategory(catId, "Membership Dues", CategoryType.Income, "1000", false));

        SetupTransactions(
            MakeTransaction(catId, "1000", debit: 0, credit: 100m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));

        var filters = CurrentYearFilters();
        var result = await _sut.GenerateAsync(filters);

        var incomeSection = result.Sections.FirstOrDefault(s => s.Heading == "Income");
        Assert.NotNull(incomeSection);
        Assert.Contains(incomeSection!.Rows, r => r.Cells[0].Contains("Membership Dues"));
    }

    [Fact]
    public async Task GenerateAsync_IncomeSection_SubtotalEqualsSum()
    {
        var catId = Guid.NewGuid();
        SetupCategories(MakeCategory(catId, "Dues", CategoryType.Income, "1000", false));
        SetupTransactions(
            MakeTransaction(catId, "1000", debit: 0, credit: 100m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(catId, "1000", debit: 0, credit: 50m, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var incomeSection = result.Sections.First(s => s.Heading == "Income");
        Assert.NotNull(incomeSection.Subtotal);
        Assert.Contains("150.00", incomeSection.Subtotal!.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_ExpenseSection_ContainsExpenseCategoryRow()
    {
        var catId = Guid.NewGuid();
        SetupCategories(MakeCategory(catId, "Hall Hire", CategoryType.Expense, "2000", false));
        SetupTransactions(
            MakeTransaction(catId, "2000", debit: 200m, credit: 0, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var expenseSection = result.Sections.FirstOrDefault(s => s.Heading == "Expenses");
        Assert.NotNull(expenseSection);
        Assert.Contains(expenseSection!.Rows, r => r.Cells[0].Contains("Hall Hire"));
    }

    [Fact]
    public async Task GenerateAsync_GrandTotal_IsNetIncome_WhenIncomeExceedsExpense()
    {
        var incCatId = Guid.NewGuid();
        var expCatId = Guid.NewGuid();
        SetupCategories(
            MakeCategory(incCatId, "Dues", CategoryType.Income, "1000", false),
            MakeCategory(expCatId, "Hall", CategoryType.Expense, "2000", false));

        SetupTransactions(
            MakeTransaction(incCatId, "1000", 0, 300m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(expCatId, "2000", 100m, 0, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.NotNull(result.GrandTotal);
        Assert.Contains("200.00", result.GrandTotal!.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_GrandTotal_IsNetLoss_WhenExpenseExceedsIncome()
    {
        var incCatId = Guid.NewGuid();
        var expCatId = Guid.NewGuid();
        SetupCategories(
            MakeCategory(incCatId, "Dues", CategoryType.Income, "1000", false),
            MakeCategory(expCatId, "Hall", CategoryType.Expense, "2000", false));

        SetupTransactions(
            MakeTransaction(incCatId, "1000", 0, 50m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(expCatId, "2000", 200m, 0, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.NotNull(result.GrandTotal);
        // Net loss = -150 (income - expense)
        Assert.Contains("-150.00", result.GrandTotal!.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_EmptySections_HandledWithoutThrowing()
    {
        SetupCategories(); // no categories
        SetupTransactions(); // no transactions

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.NotNull(result);
        Assert.Equal("Income Statement", result.Title);
    }

    [Fact]
    public async Task GenerateAsync_DateRangeFilter_Applied()
    {
        var catId = Guid.NewGuid();
        SetupCategories(MakeCategory(catId, "Dues", CategoryType.Income, "1000", false));
        SetupTransactions(); // no matching transactions for the range query

        var filters = new ReportFilterValues();
        filters.Set("dateFrom", "2026-01-01");
        filters.Set("dateTo", "2026-06-30");

        await _sut.GenerateAsync(filters);

        await _gl.Received().GetByDateRangeAsync(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private void SetupCategories(params Category[] cats)
    {
        _categories.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Category>>(cats.ToList()));
    }

    private void SetupTransactions(params Transaction[] txns)
    {
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(txns.ToList()));
    }

    private static Category MakeCategory(Guid id, string name, CategoryType type, string gl, bool isSystem)
        => new() { Id = id, Name = name, Type = type, GLAccount = gl, IsSystem = isSystem, CreatedAt = DateTime.UtcNow };

    private static Transaction MakeTransaction(Guid categoryId, string glAccount, decimal debit, decimal credit, DateTime date)
        => new() { Id = Guid.NewGuid(), CategoryId = categoryId, GLAccount = glAccount, DebitAmount = debit, CreditAmount = credit, Date = date, CreatedAt = DateTime.UtcNow };

    private static ReportFilterValues CurrentYearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{DateTime.UtcNow.Year}-01-01");
        f.Set("dateTo", $"{DateTime.UtcNow.Year}-12-31");
        return f;
    }
}
