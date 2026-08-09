using Bunit;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for AgmAttendanceGrid — member rendering, select-all, no pagination,
/// independent scroll container (FR-004/FR-005).
/// </summary>
public class AgmAttendanceGridTests : RadzenGridTestContext
{
    private static readonly List<Member> Members =
    [
        ActiveMember("Alice"),
        ActiveMember("Bob")
    ];

    [Fact]
    public void Renders_OneRowPerActiveMember()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, Members));

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    public void Default_Attended_IsUnchecked()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, Members));

        var checkboxes = cut.FindAll("input[type=checkbox][id^='agm-attended-']");
        Assert.All(checkboxes, cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void SelectAll_ChecksEveryRow()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, Members));

        cut.Find("#select-all-agm-attended").Change(true);

        var checkboxes = cut.FindAll("input[type=checkbox][id^='agm-attended-']");
        Assert.All(checkboxes, cb => Assert.True(cb.HasAttribute("checked")));
    }

    [Fact]
    public void DeselectAll_UnchecksEveryRow()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, Members));
        cut.Find("#select-all-agm-attended").Change(true);

        cut.Find("#select-all-agm-attended").Change(false);

        var checkboxes = cut.FindAll("input[type=checkbox][id^='agm-attended-']");
        Assert.All(checkboxes, cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void DoesNotRender_PaginationControls()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, Members));

        Assert.Empty(cut.FindAll(".rz-paginator"));
        Assert.Empty(cut.FindAll(".rz-pager"));
    }

    [Fact]
    public void Renders_IndependentScrollContainer()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, Members));

        var scrollContainer = cut.Find(".agm-attendance-scroll");
        Assert.Contains("overflow-y:auto", scrollContainer.GetAttribute("style"));
    }

    [Fact]
    public void GetAttendedMemberIds_ReturnsOnlyCheckedMembers()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, Members));
        var alice = Members[0];

        cut.Find($"#agm-attended-{alice.Id}").Change(true);

        var attended = cut.Instance.GetAttendedMemberIds();
        Assert.Single(attended, alice.Id);
    }

    [Fact]
    public void NoMembers_RendersWarning_NotGrid()
    {
        var cut = Render<AgmAttendanceGrid>(p => p.Add(x => x.Members, new List<Member>()));

        Assert.Contains("No active members found", cut.Markup);
        Assert.Empty(cut.FindAll("table"));
    }

    private static Member ActiveMember(string name) => new()
    {
        Id = Guid.NewGuid(), FirstName = name, LastName = "Test", StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
