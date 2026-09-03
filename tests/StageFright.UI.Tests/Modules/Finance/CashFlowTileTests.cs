using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Modules.Finance;

namespace StageFright.UI.Tests.Modules.Finance;

/// <summary>
/// bUnit tests for CashFlowTile (design 3a) — six-month income/expense bar chart
/// across loading, error, empty and populated states.
/// </summary>
public class CashFlowTileTests : LocalizedTestContext
{
    private readonly IFinanceSummaryService _summaryService = Substitute.For<IFinanceSummaryService>();

    public CashFlowTileTests()
    {
        Services.AddSingleton(_summaryService);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Should_ShowLoading_When_DataNotYetLoaded()
    {
        _summaryService.GetMonthlyCashFlowAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<IReadOnlyList<MonthlyCashFlow>>().Task);

        var cut = Render<CashFlowTile>();

        Assert.Contains("Loading", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_ServiceThrows()
    {
        _summaryService.GetMonthlyCashFlowAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<CashFlowTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_ShowEmptyMessage_When_AllMonthsHaveZeroTotals()
    {
        SetupCashFlow(MakeMonths(income: 0m, expenses: 0m));

        var cut = Render<CashFlowTile>();

        Assert.Contains("No transactions in the last 6 months", cut.Markup);
    }

    [Fact]
    public void Should_RenderChart_When_AnyMonthHasActivity()
    {
        SetupCashFlow(MakeMonths(income: 350m, expenses: 120m));

        var cut = Render<CashFlowTile>();

        Assert.DoesNotContain("No transactions", cut.Markup);
        Assert.NotEmpty(cut.FindAll("canvas"));
    }

    // --- Helpers ---

    private void SetupCashFlow(IReadOnlyList<MonthlyCashFlow> months)
    {
        _summaryService.GetMonthlyCashFlowAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(months);
    }

    private static IReadOnlyList<MonthlyCashFlow> MakeMonths(decimal income, decimal expenses)
    {
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        return Enumerable.Range(0, 6)
            .Select(i =>
            {
                var m = start.AddMonths(i);
                return new MonthlyCashFlow { Year = m.Year, Month = m.Month, Income = income, Expenses = expenses };
            })
            .ToList();
    }
}
