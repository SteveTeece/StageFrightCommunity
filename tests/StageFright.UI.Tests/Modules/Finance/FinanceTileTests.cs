using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Modules.Finance;

namespace StageFright.UI.Tests.Modules.Finance;

/// <summary>
/// bUnit tests for FinanceTile (design 3a) — current balance and month-to-date
/// income/expense lines across loading, error and populated states.
/// </summary>
public class FinanceTileTests : LocalizedTestContext
{
    private readonly IFinanceSummaryService _summaryService = Substitute.For<IFinanceSummaryService>();

    public FinanceTileTests()
    {
        Services.AddSingleton(_summaryService);
    }

    [Fact]
    public void Should_ShowLoading_When_DataNotYetLoaded()
    {
        _summaryService.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<FinanceSummary>().Task);

        var cut = Render<FinanceTile>();

        Assert.Contains("Loading", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_ServiceThrows()
    {
        _summaryService.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<FinanceTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_RenderBalanceAndMonthFigures_When_SummaryLoads()
    {
        SetupSummary(balance: 4286.50m, income: 727.50m, expenses: 306.40m);

        var cut = Render<FinanceTile>();

        Assert.Equal(4286.50m.ToString("C"), cut.Find(".tile-balance").TextContent);
        Assert.Contains("Current Balance", cut.Find(".tile-stat-label").TextContent);

        var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(DateTime.Today.Month);
        var flow = cut.Find(".tile-month-flow").TextContent;
        Assert.Contains($"{monthName} income +{727.50m.ToString("C")}", flow);
        Assert.Contains($"expenses −{306.40m.ToString("C")}", flow);
    }

    [Fact]
    public void Should_RenderZeroFigures_When_NoTransactionsExist()
    {
        SetupSummary(0m, 0m, 0m);

        var cut = Render<FinanceTile>();

        Assert.Equal(0m.ToString("C"), cut.Find(".tile-balance").TextContent);
    }

    // --- Helpers ---

    private void SetupSummary(decimal balance, decimal income, decimal expenses)
    {
        _summaryService.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new FinanceSummary
            {
                CurrentBalance = balance,
                MonthIncome = income,
                MonthExpenses = expenses
            });
    }
}
