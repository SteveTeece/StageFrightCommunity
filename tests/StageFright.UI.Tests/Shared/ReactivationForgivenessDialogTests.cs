using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for ReactivationForgivenessDialog:
/// - Prior-year fees are pre-checked
/// - Current-year fees are unchecked
/// - Fees are grouped by year (year heading per group)
/// - Confirm calls ApplyForgivenessAsync with selected fee IDs
/// - Empty fee list shows no-fees message
/// </summary>
public class ReactivationForgivenessDialogTests : BunitContext
{
    private readonly IReactivationForgivenessService _forgivenessService =
        Substitute.For<IReactivationForgivenessService>();

    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid PriorFeeId = Guid.NewGuid();
    private static readonly Guid CurrentFeeId = Guid.NewGuid();
    private static readonly int CurrentYear = DateTime.UtcNow.Year;

    public ReactivationForgivenessDialogTests()
    {
        Services.AddSingleton(_forgivenessService);

        _forgivenessService.GetForgivenessItemsAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<ForgivenessItem>
            {
                new() { FeeId = PriorFeeId, Year = CurrentYear - 1, FeeDate = new DateTime(CurrentYear - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc), Amount = 50m, IsDefaultForgiven = true },
                new() { FeeId = CurrentFeeId, Year = CurrentYear, FeeDate = new DateTime(CurrentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc), Amount = 50m, IsDefaultForgiven = false }
            });

        _forgivenessService.ApplyForgivenessAsync(
            MemberId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    // --- Checkbox state ---

    [Fact]
    public void PriorYearFee_IsCheckedByDefault()
    {
        var cut = RenderDialog();

        var checkbox = cut.Find($"#fee-{PriorFeeId}");
        Assert.True(checkbox.HasAttribute("checked") || checkbox.GetAttribute("checked") == "true",
            "Prior-year fee checkbox should be pre-checked");
    }

    [Fact]
    public void CurrentYearFee_IsUncheckedByDefault()
    {
        var cut = RenderDialog();

        var checkbox = cut.Find($"#fee-{CurrentFeeId}");
        Assert.False(checkbox.HasAttribute("checked"),
            "Current-year fee checkbox should be unchecked by default");
    }

    // --- Year grouping ---

    [Fact]
    public void Renders_YearHeading_ForEachGroup()
    {
        var cut = RenderDialog();

        var headings = cut.FindAll("h6");
        Assert.Equal(2, headings.Count);

        var headingTexts = headings.Select(h => h.TextContent).ToList();
        Assert.Contains((CurrentYear - 1).ToString(), headingTexts);
        Assert.Contains(CurrentYear.ToString(), headingTexts);
    }

    [Fact]
    public void Renders_AllFeeCheckboxes()
    {
        var cut = RenderDialog();

        // Two fees → two checkboxes
        Assert.Equal(2, cut.FindAll("input[type=checkbox]").Count);
    }

    // --- Apply ---

    [Fact]
    public async Task ClickConfirm_CallsApplyForgivenessAsync_WithSelectedFees()
    {
        var cut = RenderDialog();

        var confirmBtn = cut.Find("button.btn-primary");
        await confirmBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Only PriorFeeId was pre-checked by default
        await _forgivenessService.Received(1).ApplyForgivenessAsync(
            MemberId,
            Arg.Is<IReadOnlyList<Guid>>(ids => ids!.Contains(PriorFeeId) && !ids!.Contains(CurrentFeeId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckingCurrentYearFee_IncludesItInApply()
    {
        var cut = RenderDialog();

        // Check the current-year fee
        var currentCheckbox = cut.Find($"#fee-{CurrentFeeId}");
        await currentCheckbox.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

        var confirmBtn = cut.Find("button.btn-primary");
        await confirmBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _forgivenessService.Received(1).ApplyForgivenessAsync(
            MemberId,
            Arg.Is<IReadOnlyList<Guid>>(ids => ids!.Contains(PriorFeeId) && ids!.Contains(CurrentFeeId)),
            Arg.Any<CancellationToken>());
    }

    // --- Empty state ---

    [Fact]
    public void WhenNoFees_ShowsNoFeesMessage()
    {
        _forgivenessService.GetForgivenessItemsAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<ForgivenessItem>());

        var cut = RenderDialog();

        Assert.Contains("No outstanding fees", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers ---

    private IRenderedComponent<ReactivationForgivenessDialog> RenderDialog()
    {
        return Render<ReactivationForgivenessDialog>(p => p
            .Add(x => x.MemberId, MemberId)
            .Add(x => x.IsVisible, true));
    }
}
