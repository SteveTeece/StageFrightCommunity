using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for FinanceSummaryService — verifies balance/month figures and monthly
/// cash-flow buckets follow the income-statement conventions (non-system categories only,
/// income = credits − debits, expenses = debits − credits).
/// </summary>
public class FinanceSummaryServiceTests
{
    private static readonly DateTime AsOf = new(2026, 7, 4, 0, 0, 0, DateTimeKind.Utc);

    private readonly IGLRepository _glRepository = Substitute.For<IGLRepository>();
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
    private readonly FinanceSummaryService _service;

    private readonly Category _incomeCategory = MakeCategory(CategoryType.Income, isSystem: false);
    private readonly Category _expenseCategory = MakeCategory(CategoryType.Expense, isSystem: false);
    private readonly Category _systemIncomeCategory = MakeCategory(CategoryType.Income, isSystem: true);
    private readonly Category _systemExpenseCategory = MakeCategory(CategoryType.Expense, isSystem: true);

    public FinanceSummaryServiceTests()
    {
        _service = new FinanceSummaryService(_glRepository, _categoryRepository);
        _categoryRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([_incomeCategory, _expenseCategory, _systemIncomeCategory, _systemExpenseCategory]);
    }

    // --- GetSummaryAsync ---

    [Fact]
    public async Task Should_ComputeBalanceAsIncomeMinusExpenses_When_TransactionsExist()
    {
        SetupTransactions(
            Txn(_incomeCategory.Id, credit: 500m, date: AsOf.AddMonths(-3)),
            Txn(_incomeCategory.Id, credit: 250m, date: AsOf.AddDays(-1)),
            Txn(_expenseCategory.Id, debit: 100m, date: AsOf.AddMonths(-2)));

        var summary = await _service.GetSummaryAsync(AsOf);

        Assert.Equal(650m, summary.CurrentBalance);
    }

    [Fact]
    public async Task Should_ExcludeSystemCategories_When_ComputingSummary()
    {
        SetupTransactions(
            Txn(_incomeCategory.Id, credit: 100m, date: AsOf.AddDays(-1)),
            Txn(_systemIncomeCategory.Id, credit: 9_999m, date: AsOf.AddDays(-1)),
            Txn(_systemExpenseCategory.Id, debit: 5_000m, date: AsOf.AddDays(-1)));

        var summary = await _service.GetSummaryAsync(AsOf);

        Assert.Equal(100m, summary.CurrentBalance);
        Assert.Equal(100m, summary.MonthIncome);
        Assert.Equal(0m, summary.MonthExpenses);
    }

    [Fact]
    public async Task Should_NetDebitsAgainstCredits_When_CategoryHasBothLegs()
    {
        SetupTransactions(
            Txn(_incomeCategory.Id, credit: 300m, date: AsOf.AddDays(-2)),
            Txn(_incomeCategory.Id, debit: 50m, date: AsOf.AddDays(-2)),   // income reversal
            Txn(_expenseCategory.Id, debit: 200m, date: AsOf.AddDays(-2)),
            Txn(_expenseCategory.Id, credit: 40m, date: AsOf.AddDays(-2))); // expense refund

        var summary = await _service.GetSummaryAsync(AsOf);

        Assert.Equal(250m, summary.MonthIncome);
        Assert.Equal(160m, summary.MonthExpenses);
        Assert.Equal(90m, summary.CurrentBalance);
    }

    [Fact]
    public async Task Should_LimitMonthFiguresToAsOfMonth_When_OlderTransactionsExist()
    {
        SetupTransactions(
            Txn(_incomeCategory.Id, credit: 400m, date: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)),
            Txn(_incomeCategory.Id, credit: 150m, date: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            Txn(_expenseCategory.Id, debit: 60m, date: new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc)));

        var summary = await _service.GetSummaryAsync(AsOf);

        Assert.Equal(150m, summary.MonthIncome);
        Assert.Equal(60m, summary.MonthExpenses);
        Assert.Equal(490m, summary.CurrentBalance);
    }

    [Fact]
    public async Task Should_ReturnZeroSummary_When_NoTransactionsExist()
    {
        SetupTransactions();

        var summary = await _service.GetSummaryAsync(AsOf);

        Assert.Equal(0m, summary.CurrentBalance);
        Assert.Equal(0m, summary.MonthIncome);
        Assert.Equal(0m, summary.MonthExpenses);
    }

    [Fact]
    public async Task Should_QueryThroughEndOfAsOfDay_When_GettingSummary()
    {
        SetupTransactions();

        await _service.GetSummaryAsync(AsOf);

        await _glRepository.Received(1).GetByDateRangeAsync(
            Arg.Is<DateTime>(d => d == DateTime.MinValue),
            Arg.Is<DateTime>(d => d.Date == AsOf.Date && d.Hour == 23 && d.Minute == 59),
            Arg.Any<CancellationToken>());
    }

    // --- GetMonthlyCashFlowAsync ---

    [Fact]
    public async Task Should_ReturnOneEntryPerMonthOldestFirst_When_GettingCashFlow()
    {
        SetupTransactions();

        var result = await _service.GetMonthlyCashFlowAsync(AsOf, 6);

        Assert.Equal(6, result.Count);
        Assert.Equal((2026, 2), (result[0].Year, result[0].Month));
        Assert.Equal((2026, 7), (result[5].Year, result[5].Month));
    }

    [Fact]
    public async Task Should_BucketTransactionsByMonth_When_GettingCashFlow()
    {
        SetupTransactions(
            Txn(_incomeCategory.Id, credit: 300m, date: new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc)),
            Txn(_expenseCategory.Id, debit: 120m, date: new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc)),
            Txn(_incomeCategory.Id, credit: 80m, date: new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _service.GetMonthlyCashFlowAsync(AsOf, 6);

        var may = result.Single(m => m.Month == 5);
        var july = result.Single(m => m.Month == 7);
        Assert.Equal(300m, may.Income);
        Assert.Equal(120m, may.Expenses);
        Assert.Equal(80m, july.Income);
        Assert.Equal(0m, july.Expenses);
    }

    [Fact]
    public async Task Should_ZeroFillMonths_When_MonthHasNoTransactions()
    {
        SetupTransactions(
            Txn(_incomeCategory.Id, credit: 100m, date: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _service.GetMonthlyCashFlowAsync(AsOf, 3);

        Assert.Equal(0m, result[0].Income);
        Assert.Equal(0m, result[0].Expenses);
        Assert.Equal(0m, result[1].Income);
        Assert.Equal(100m, result[2].Income);
    }

    [Fact]
    public async Task Should_SpanYearBoundary_When_WindowCrossesJanuary()
    {
        var asOf = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        SetupTransactions();

        var result = await _service.GetMonthlyCashFlowAsync(asOf, 4);

        Assert.Equal((2025, 11), (result[0].Year, result[0].Month));
        Assert.Equal((2025, 12), (result[1].Year, result[1].Month));
        Assert.Equal((2026, 1), (result[2].Year, result[2].Month));
        Assert.Equal((2026, 2), (result[3].Year, result[3].Month));
    }

    [Fact]
    public async Task Should_ExcludeSystemCategories_When_GettingCashFlow()
    {
        SetupTransactions(
            Txn(_systemIncomeCategory.Id, credit: 500m, date: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _service.GetMonthlyCashFlowAsync(AsOf, 2);

        Assert.All(result, m => Assert.Equal(0m, m.Income));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Should_ThrowArgumentOutOfRange_When_MonthsIsNotPositive(int months)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetMonthlyCashFlowAsync(AsOf, months));
    }

    // --- Helpers ---

    private void SetupTransactions(params Transaction[] transactions)
    {
        _glRepository.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(transactions);
    }

    private static Category MakeCategory(CategoryType type, bool isSystem) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{type}{(isSystem ? "-system" : "")}",
        Type = type,
        IsSystem = isSystem,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Transaction Txn(Guid categoryId, decimal debit = 0m, decimal credit = 0m, DateTime date = default) => new()
    {
        Id = Guid.NewGuid(),
        CategoryId = categoryId,
        DebitAmount = debit,
        CreditAmount = credit,
        Date = date,
        CreatedAt = DateTime.UtcNow
    };
}
