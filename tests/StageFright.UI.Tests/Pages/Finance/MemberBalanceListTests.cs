using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for MemberBalanceList — the Outstanding tab's member balance grid.
/// Verifies rendering, fee-breakdown expand/collapse, and that "Record Member Payment"
/// navigates back to the Outstanding tab itself (with memberId set) rather than a
/// separate tab, since that standalone tab was removed.
/// </summary>
public class MemberBalanceListTests : BunitContext
{
    private readonly IMemberBalanceService _balanceService = Substitute.For<IMemberBalanceService>();

    private static readonly Guid MemberId = Guid.NewGuid();

    public MemberBalanceListTests()
    {
        Services.AddSingleton(_balanceService);
    }

    [Fact]
    public void Should_ShowEmptyMessage_When_NoBalancesExist()
    {
        _balanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MemberBalance>());

        var cut = Render<MemberBalanceList>();

        Assert.Contains("No outstanding balances found.", cut.Markup);
    }

    [Fact]
    public void Should_RenderMemberBalance_When_BalancesExist()
    {
        _balanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MemberBalance>
            {
                new()
                {
                    MemberId = MemberId,
                    Name = "Amanda Scott",
                    Balance = 45m,
                    Fees = [MakeFee(MemberId, 45m)]
                }
            });

        var cut = Render<MemberBalanceList>();

        Assert.Contains("Amanda Scott", cut.Markup);
        Assert.Contains("$45.00", cut.Markup);
    }

    [Fact]
    public void Should_ShowErrorMessage_When_LoadFails()
    {
        _balanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<MemberBalance>>>(_ => throw new InvalidOperationException("boom"));

        var cut = Render<MemberBalanceList>();

        Assert.Contains("Failed to load balances", cut.Markup);
    }

    [Fact]
    public void Should_ToggleFeeBreakdown_When_ShowFeesClicked()
    {
        _balanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MemberBalance>
            {
                new()
                {
                    MemberId = MemberId,
                    Name = "Amanda Scott",
                    Balance = 45m,
                    Fees = [MakeFee(MemberId, 45m)]
                }
            });

        var cut = Render<MemberBalanceList>();
        cut.Find("button.btn-link").Click();

        Assert.Contains("Hide fees", cut.Markup);
    }

    [Fact]
    public void GoToPayment_NavigatesToOutstandingTab_WithMemberId()
    {
        _balanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MemberBalance>
            {
                new()
                {
                    MemberId = MemberId,
                    Name = "Amanda Scott",
                    Balance = 45m,
                    Fees = [MakeFee(MemberId, 45m)]
                }
            });

        var cut = Render<MemberBalanceList>();
        cut.FindAll("button").First(b => b.TextContent.Contains("Record Member Payment")).Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/finance?tab=outstanding&memberId={MemberId}", nav.Uri);
    }

    // --- Helpers ---

    private static Fee MakeFee(Guid memberId, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        MemberId = memberId,
        FeeType = FeeType.Annual,
        Amount = amount,
        FeeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        PaidAtCreation = false,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };
}
