using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Modules.Finance;

namespace StageFright.UI.Tests.Modules.Finance;

/// <summary>
/// bUnit tests for OutstandingBalancesTile (design 4) — member count and per-fee-type
/// outstanding totals across loading, error, zero-state, and populated states.
/// </summary>
public class OutstandingBalancesTileTests : BunitContext
{
    private readonly IMemberBalanceService _memberBalanceService = Substitute.For<IMemberBalanceService>();
    private readonly IFinanceSummaryService _summaryService = Substitute.For<IFinanceSummaryService>();

    public OutstandingBalancesTileTests()
    {
        Services.AddSingleton(_memberBalanceService);
        Services.AddSingleton(_summaryService);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _summaryService.GetOutstandingFeeSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(new OutstandingFeeSummary());
        _summaryService.GetOutstandingBalanceTrendAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MonthlyOutstandingBalance>)[]);
    }

    [Fact]
    public void Should_ShowLoading_When_DataNotYetLoaded()
    {
        _memberBalanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<IReadOnlyList<MemberBalance>>().Task);

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains("Loading", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_MemberBalanceServiceThrows()
    {
        _memberBalanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_FinanceSummaryServiceThrows()
    {
        SetupBalances();
        _summaryService.GetOutstandingFeeSummaryAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_ShowZeroValues_When_NoOutstandingBalancesExist()
    {
        SetupBalances();
        _summaryService.GetOutstandingFeeSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(new OutstandingFeeSummary { OutstandingAttendanceFees = 0m, OutstandingAnnualFees = 0m });

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains("0", cut.Markup);
        Assert.Contains("$0.00", cut.Markup);
        Assert.DoesNotContain("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_ShowMemberCountAndFeeTypeTotals_When_BalancesExist()
    {
        SetupBalances(MakeBalance(), MakeBalance(), MakeBalance());
        _summaryService.GetOutstandingFeeSummaryAsync(Arg.Any<CancellationToken>())
            .Returns(new OutstandingFeeSummary { OutstandingAttendanceFees = 45.50m, OutstandingAnnualFees = 300m });

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains("3", cut.Markup);
        Assert.Contains("$45.50", cut.Markup);
        Assert.Contains("$300.00", cut.Markup);
    }

    [Fact]
    public void Should_CountMemberOnce_When_OwingBothFeeTypes()
    {
        // One MemberBalance entry represents the member regardless of how many fee types
        // contribute to it — MemberBalanceService already collapses this per-member.
        SetupBalances(MakeBalance());

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains(">1<", cut.Markup);
    }

    [Fact]
    public void Should_RenderChart_When_TrendHasNonZeroData()
    {
        SetupBalances();
        _summaryService.GetOutstandingBalanceTrendAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(MakeTrend(0m, 50m, 120m));

        var cut = Render<OutstandingBalancesTile>();

        Assert.DoesNotContain("No outstanding balances this year", cut.Markup);
        Assert.NotEmpty(cut.FindAll("canvas"));
    }

    [Fact]
    public void Should_ShowNote_When_TrendIsAllZero()
    {
        SetupBalances();
        _summaryService.GetOutstandingBalanceTrendAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(MakeTrend(0m, 0m, 0m));

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains("No outstanding balances this year", cut.Markup);
        Assert.Empty(cut.FindAll("canvas"));
    }

    [Fact]
    public void Should_ShowStatsAndNote_When_NoOutstandingBalancesAtAll()
    {
        // FR-008: stats always render, even when the chart degrades to the zero-state note.
        SetupBalances();

        var cut = Render<OutstandingBalancesTile>();

        Assert.Contains("0", cut.Markup);
        Assert.Contains("$0.00", cut.Markup);
        Assert.Contains("No outstanding balances this year", cut.Markup);
    }

    // --- Helpers ---

    private static IReadOnlyList<MonthlyOutstandingBalance> MakeTrend(params decimal[] monthlyBalances) =>
        monthlyBalances
            .Select((balance, i) => new MonthlyOutstandingBalance { Year = 2026, Month = i + 1, OutstandingBalance = balance })
            .ToList();

    private void SetupBalances(params MemberBalance[] balances)
    {
        _memberBalanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MemberBalance>)balances);
    }

    private static MemberBalance MakeBalance() => new()
    {
        MemberId = Guid.NewGuid(),
        Name = "Test Member",
        Balance = 50m,
        Fees =
        [
            new Fee { Id = Guid.NewGuid(), FeeType = FeeType.Annual, Amount = 30m },
            new Fee { Id = Guid.NewGuid(), FeeType = FeeType.Attendance, Amount = 20m }
        ]
    };
}
