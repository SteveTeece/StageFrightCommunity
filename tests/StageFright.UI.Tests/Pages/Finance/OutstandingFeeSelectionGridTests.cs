using Bunit;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for OutstandingFeeSelectionGrid: initial render, per-row and select-all
/// checkbox toggling, SelectionChanged sum, GetSelectedFeeIds, empty state, ReadOnly disabling.
/// </summary>
public class OutstandingFeeSelectionGridTests : RadzenGridTestContext
{
    private static readonly Guid Fee1Id = Guid.NewGuid();
    private static readonly Guid Fee2Id = Guid.NewGuid();

    private static readonly List<OutstandingFee> TwoFees =
    [
        new()
        {
            FeeId = Fee1Id, FeeType = FeeType.Annual,
            FeeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            RemainingAmount = 30m
        },
        new()
        {
            FeeId = Fee2Id, FeeType = FeeType.Attendance,
            FeeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            RemainingAmount = 80m
        }
    ];

    [Fact]
    public void InitialRender_AllRowsUnchecked()
    {
        var cut = Render<OutstandingFeeSelectionGrid>(p => p.Add(x => x.Fees, TwoFees));

        var checkboxes = cut.FindAll("input[type=checkbox][id^='fee-']");
        Assert.Equal(2, checkboxes.Count);
        Assert.All(checkboxes, cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void PerRowToggle_RaisesSelectionChanged_WithCheckedSum()
    {
        decimal? raisedSum = null;
        var cut = Render<OutstandingFeeSelectionGrid>(p => p
            .Add(x => x.Fees, TwoFees)
            .Add(x => x.SelectionChanged, sum => raisedSum = sum));

        cut.Find($"#fee-{Fee1Id}").Change(true);

        Assert.Equal(30m, raisedSum);
    }

    [Fact]
    public void HeaderSelectAll_ChecksEveryRow_AndRaisesSelectionChanged_WithTotalSum()
    {
        decimal? raisedSum = null;
        var cut = Render<OutstandingFeeSelectionGrid>(p => p
            .Add(x => x.Fees, TwoFees)
            .Add(x => x.SelectionChanged, sum => raisedSum = sum));

        cut.Find("#select-all-outstanding-fees").Change(true);

        var checkboxes = cut.FindAll("input[type=checkbox][id^='fee-']");
        Assert.All(checkboxes, cb => Assert.True(cb.HasAttribute("checked")));
        Assert.Equal(110m, raisedSum);
    }

    [Fact]
    public void HeaderSelectAll_ThenUncheck_UnchecksEveryRow_AndRaisesZero()
    {
        decimal? raisedSum = null;
        var cut = Render<OutstandingFeeSelectionGrid>(p => p
            .Add(x => x.Fees, TwoFees)
            .Add(x => x.SelectionChanged, sum => raisedSum = sum));

        cut.Find("#select-all-outstanding-fees").Change(true);
        cut.Find("#select-all-outstanding-fees").Change(false);

        var checkboxes = cut.FindAll("input[type=checkbox][id^='fee-']");
        Assert.All(checkboxes, cb => Assert.False(cb.HasAttribute("checked")));
        Assert.Equal(0m, raisedSum);
    }

    [Fact]
    public void GetSelectedFeeIds_ReturnsExactlyCheckedFeeIds()
    {
        var cut = Render<OutstandingFeeSelectionGrid>(p => p.Add(x => x.Fees, TwoFees));

        cut.Find($"#fee-{Fee2Id}").Change(true);

        var selected = cut.Instance.GetSelectedFeeIds();
        Assert.Single(selected);
        Assert.Equal(Fee2Id, selected[0]);
    }

    [Fact]
    public void EmptyFees_RendersEmptyStateMessage()
    {
        var cut = Render<OutstandingFeeSelectionGrid>(p => p.Add(x => x.Fees, new List<OutstandingFee>()));

        Assert.Contains("no outstanding fees", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadOnly_DisablesAllCheckboxes()
    {
        var cut = Render<OutstandingFeeSelectionGrid>(p => p
            .Add(x => x.Fees, TwoFees)
            .Add(x => x.ReadOnly, true));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.NotEmpty(checkboxes);
        Assert.All(checkboxes, cb => Assert.True(cb.HasAttribute("disabled")));
    }
}
