using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Members;

namespace StageFright.UI.Tests.Pages.Members;

/// <summary>
/// bUnit tests for MemberList — the show-inactive switch and its effect on the grid.
/// </summary>
public class MemberListTests : BunitContext
{
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();

    private readonly Member _activeMember = new()
    {
        Id = Guid.NewGuid(), Name = "Alice Active", StreetAddress = "1 Test St",
        Status = MemberStatus.Active, JoinDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private readonly Member _inactiveMember = new()
    {
        Id = Guid.NewGuid(), Name = "Bob Inactive", StreetAddress = "2 Test St",
        Status = MemberStatus.Inactive, JoinDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    public MemberListTests()
    {
        Services.AddSingleton(_memberService);

        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member> { _activeMember });
        _memberService.GetByStatusAsync(MemberStatus.Inactive, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member> { _inactiveMember });
    }

    [Fact]
    public void DefaultLoad_ShowsOnlyActiveMembers_AndSwitchIsUnchecked()
    {
        var cut = Render<MemberList>();

        var switchInput = cut.Find("#showInactiveSwitch");
        Assert.False(switchInput.HasAttribute("checked"));

        Assert.Contains("Alice Active", cut.Markup);
        Assert.DoesNotContain("Bob Inactive", cut.Markup);
    }

    [Fact]
    public void TogglingSwitch_ShowsInactiveMembers_WithInactiveSuffix()
    {
        var cut = Render<MemberList>();

        cut.Find("#showInactiveSwitch").Change(true);

        Assert.Contains("Alice Active", cut.Markup);
        Assert.Contains("Bob Inactive (Inactive)", cut.Markup);
    }

    [Fact]
    public void TogglingSwitchOffAgain_HidesInactiveMembers()
    {
        var cut = Render<MemberList>();

        cut.Find("#showInactiveSwitch").Change(true);
        cut.Find("#showInactiveSwitch").Change(false);

        Assert.Contains("Alice Active", cut.Markup);
        Assert.DoesNotContain("Bob Inactive", cut.Markup);
    }

    [Fact]
    public void NoActiveMembers_AndSwitchOff_ShowsActiveEmptyMessage()
    {
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member>());

        var cut = Render<MemberList>();

        Assert.Contains("No active members found.", cut.Markup);
    }

    [Fact]
    public void NoMembersAtAll_AndSwitchOn_ShowsGenericEmptyMessage()
    {
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member>());
        _memberService.GetByStatusAsync(MemberStatus.Inactive, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Member>)new List<Member>());

        var cut = Render<MemberList>();

        cut.Find("#showInactiveSwitch").Change(true);

        Assert.Contains("No members found.", cut.Markup);
        Assert.DoesNotContain("No active members found.", cut.Markup);
    }
}
