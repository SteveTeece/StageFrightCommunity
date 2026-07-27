using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Modules.Members;

namespace StageFright.UI.Tests.Modules.Members;

/// <summary>
/// bUnit tests for MembersTile (design 3a) — active/inactive/total stats and the
/// outstanding-fees chip across loading, error, zero and populated states.
/// </summary>
public class MembersTileTests : BunitContext
{
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly IMemberBalanceService _balanceService = Substitute.For<IMemberBalanceService>();

    public MembersTileTests()
    {
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_balanceService);
    }

    [Fact]
    public void Should_ShowLoading_When_DataNotYetLoaded()
    {
        _memberService.GetByStatusAsync(Arg.Any<MemberStatus>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<IReadOnlyList<Member>>().Task);

        var cut = Render<MembersTile>();

        Assert.Contains("Loading", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_ServiceThrows()
    {
        _memberService.GetByStatusAsync(Arg.Any<MemberStatus>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<MembersTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_RenderActiveInactiveAndTotalCounts_When_MembersExist()
    {
        SetupMembers(activeCount: 24, inactiveCount: 3);
        SetupOutstanding(0);

        var cut = Render<MembersTile>();

        var values = cut.FindAll(".tile-stat-value").Select(v => v.TextContent).ToList();
        Assert.Equal(["24", "3", "27"], values);
        var labels = cut.FindAll(".tile-stat-label").Select(l => l.TextContent).ToList();
        Assert.Equal(["Active", "Inactive", "Total"], labels);
    }

    [Fact]
    public void Should_ShowOutstandingChipWithPlural_When_MultipleMembersOwe()
    {
        SetupMembers(1, 0);
        SetupOutstanding(8);

        var cut = Render<MembersTile>();

        var chip = cut.Find(".tile-chip-alert");
        Assert.Contains("Outstanding fees", chip.TextContent);
        Assert.Contains("8 members", chip.TextContent);
    }

    [Fact]
    public void Should_ShowOutstandingChipWithSingular_When_OneMemberOwes()
    {
        SetupMembers(1, 0);
        SetupOutstanding(1);

        var cut = Render<MembersTile>();

        Assert.Contains("1 member", cut.Find(".tile-chip-alert").TextContent);
        Assert.DoesNotContain("1 members", cut.Find(".tile-chip-alert").TextContent);
    }

    [Fact]
    public void Should_ShowNoOutstandingNote_When_NoMemberOwes()
    {
        SetupMembers(2, 1);
        SetupOutstanding(0);

        var cut = Render<MembersTile>();

        Assert.Empty(cut.FindAll(".tile-chip-alert"));
        Assert.Contains("No outstanding fees", cut.Markup);
    }

    // --- Helpers ---

    private void SetupMembers(int activeCount, int inactiveCount)
    {
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(MakeMembers(activeCount));
        _memberService.GetByStatusAsync(MemberStatus.Inactive, Arg.Any<CancellationToken>())
            .Returns(MakeMembers(inactiveCount));
    }

    private void SetupOutstanding(int count)
    {
        var balances = Enumerable.Range(0, count)
            .Select(i => new MemberBalance { MemberId = Guid.NewGuid(), Name = $"M{i}", Balance = 10m })
            .ToList();
        _balanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MemberBalance>)balances);
    }

    private static IReadOnlyList<Member> MakeMembers(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Member { Id = Guid.NewGuid(), FirstName = $"Member {i}" })
            .ToList();
}
