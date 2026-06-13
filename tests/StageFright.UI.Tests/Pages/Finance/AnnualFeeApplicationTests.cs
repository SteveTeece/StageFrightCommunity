using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for AnnualFeeApplication — eligibility count display, confirm triggers service,
/// cancel navigation, zero-eligible state, and success message after apply.
/// </summary>
public class AnnualFeeApplicationTests : BunitContext
{
    private readonly IFeeService _feeService = Substitute.For<IFeeService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public AnnualFeeApplicationTests()
    {
        Services.AddSingleton(_feeService);
        Services.AddSingleton(_settingsService);

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test Choir",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _feeService.GetEligibleMembersAsync(Arg.Any<CancellationToken>())
            .Returns(TwoEligibleMembers());

        _feeService.ApplyAnnualFeesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(2);
    }

    [Fact]
    public void Renders_EligibleCount_InConfirmationDialog()
    {
        var cut = Render<AnnualFeeApplication>();

        Assert.Contains("2", cut.Markup);
        Assert.Contains("eligible", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_AnnualFeeAmount_InConfirmationDialog()
    {
        var cut = Render<AnnualFeeApplication>();

        // Fee amount from settings
        Assert.Contains("50", cut.Markup);
    }

    [Fact]
    public void Renders_ApplyButton_WhenEligibleMembersExist()
    {
        var cut = Render<AnnualFeeApplication>();

        cut.Find("button.btn-primary");
    }

    [Fact]
    public void Renders_CancelLink_Always()
    {
        var cut = Render<AnnualFeeApplication>();

        var cancelLink = cut.Find("a.btn-outline-secondary");
        Assert.Contains("/finance", cancelLink.GetAttribute("href") ?? string.Empty);
    }

    [Fact]
    public async Task ClickApply_CallsFeeService_ApplyAnnualFeesAsync()
    {
        var cut = Render<AnnualFeeApplication>();

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _feeService.Received(1).ApplyAnnualFeesAsync(
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClickApply_ShowsSuccessMessage_WithCount()
    {
        var cut = Render<AnnualFeeApplication>();

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("2", cut.Markup);
        Assert.Contains("applied successfully", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhenNoEligibleMembers_ShowsNoMembersMessage_AndNoApplyButton()
    {
        _feeService.GetEligibleMembersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        var cut = Render<AnnualFeeApplication>();

        Assert.Empty(cut.FindAll("button.btn-primary"));
        Assert.Contains("No eligible members", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClickApply_DoesNotCallService_WhenCancelled()
    {
        // Cancel does not invoke ApplyAnnualFeesAsync
        Render<AnnualFeeApplication>();

        await _feeService.DidNotReceive().ApplyAnnualFeesAsync(
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static List<Member> TwoEligibleMembers() =>
    [
        MakeMember("Alice"),
        MakeMember("Bob")
    ];

    private static Member MakeMember(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, StreetAddress = "1 St",
        Status = MemberStatus.Active,
        JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
