using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Events;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for ParticipationGrid — member rendering, no-fee columns, save behavior, locked state.
/// </summary>
public class ParticipationGridTests : RadzenGridTestContext
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();

    private static readonly Guid EventId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid EventTypeId = Guid.NewGuid();

    private static readonly Event OpenEvent = new()
    {
        Id = EventId,
        Date = DateTime.UtcNow.Date.AddDays(-1),
        EventTypeId = EventTypeId,
        EventType = new EventType { Id = EventTypeId, Name = "Performance" },
        StoredParticipationRate = null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static readonly Event LockedEvent = new()
    {
        Id = EventId,
        Date = DateTime.UtcNow.Date.AddDays(-1),
        EventTypeId = EventTypeId,
        EventType = new EventType { Id = EventTypeId, Name = "Performance" },
        StoredParticipationRate = 75m,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static readonly Event FutureEvent = new()
    {
        Id = EventId,
        Date = DateTime.Today.AddDays(1),
        EventTypeId = EventTypeId,
        EventType = new EventType { Id = EventTypeId, Name = "Performance" },
        StoredParticipationRate = null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public ParticipationGridTests()
    {
        Services.AddSingleton(_eventService);
        Services.AddSingleton(_memberService);
        Services.AddSingleton(Substitute.For<Microsoft.AspNetCore.Components.NavigationManager>());

        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event> { OpenEvent });

        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member>
            {
                ActiveMember("Alice"),
                ActiveMember("Bob")
            });

        _memberService.GetByStatusAsync(MemberStatus.Inactive, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());
    }

    [Fact]
    public void Renders_AllMembers_InGrid()
    {
        var cut = RenderWithId();

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    public void Default_Participated_IsUnchecked()
    {
        var cut = RenderWithId();

        var checkboxes = cut.FindAll("input[type=checkbox][id^='participated-']");
        Assert.All(checkboxes, cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void DoesNotRender_FeeColumns()
    {
        var cut = RenderWithId();

        Assert.DoesNotContain("Fee", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unpaid", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mark as", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Select All ---

    [Fact]
    public void Renders_SelectAllCheckbox_BelowParticipatedColumnTitle_InParentheses()
    {
        var cut = RenderWithId();

        var header = cut.Find("thead th:last-child");
        Assert.NotNull(header.QuerySelector("#select-all-participants"));
        Assert.Contains("Select All", header.TextContent);
        Assert.Contains("Participated", header.TextContent);
        Assert.True(
            header.TextContent.IndexOf("Participated", StringComparison.Ordinal) <
            header.TextContent.IndexOf("Select All", StringComparison.Ordinal),
            "Participated should appear before the Select All control in the header's text content.");

        var inline = header.QuerySelector(".select-all-inline");
        Assert.NotNull(inline);
        Assert.StartsWith("(", inline!.TextContent.TrimStart());
        Assert.EndsWith(")", inline.TextContent.TrimEnd());
    }

    [Fact]
    public void CheckingSelectAll_ChecksAllRowCheckboxes()
    {
        var cut = RenderWithId();

        cut.Find("#select-all-participants").Change(true);

        var checkboxes = cut.FindAll("input[type=checkbox][id^='participated-']");
        Assert.All(checkboxes, cb => Assert.True(cb.HasAttribute("checked")));
    }

    [Fact]
    public void UncheckingSelectAll_ClearsAllRowCheckboxes()
    {
        var cut = RenderWithId();
        cut.Find("#select-all-participants").Change(true);

        cut.Find("#select-all-participants").Change(false);

        var checkboxes = cut.FindAll("input[type=checkbox][id^='participated-']");
        Assert.All(checkboxes, cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void UncheckingOneRow_AfterSelectAll_UnchecksSelectAll()
    {
        var cut = RenderWithId();
        cut.Find("#select-all-participants").Change(true);

        cut.Find("input[type=checkbox][id^='participated-']").Change(false);

        Assert.False(cut.Find("#select-all-participants").HasAttribute("checked"));
    }

    [Fact]
    public void CheckingEveryRowIndividually_ChecksSelectAll()
    {
        var cut = RenderWithId();

        var count = cut.FindAll("input[type=checkbox][id^='participated-']").Count;
        for (var i = 0; i < count; i++)
            cut.FindAll("input[type=checkbox][id^='participated-']")[i].Change(true);

        Assert.True(cut.Find("#select-all-participants").HasAttribute("checked"));
    }

    [Fact]
    public async Task ClickSave_AfterSelectAll_SendsAllParticipantsAsParticipated()
    {
        var cut = RenderWithId();
        cut.Find("#select-all-participants").Change(true);

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _eventService.Received(1).RecordParticipationAsync(
            EventId,
            Arg.Is<IReadOnlyList<ParticipationBatchItem>>(items =>
                items.Count == 2 && items.All(i => i.Participated)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Renders_SaveButton()
    {
        var cut = RenderWithId();
        cut.Find("button.btn-primary");
    }

    [Fact]
    public async Task ClickSave_CallsEventService_RecordParticipationAsync()
    {
        var cut = RenderWithId();

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _eventService.Received(1).RecordParticipationAsync(
            EventId,
            Arg.Any<IReadOnlyList<ParticipationBatchItem>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void WhenParticipationAlreadyRecorded_ShowsLockedMessage_AndNoSaveButton()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event> { LockedEvent });

        var cut = RenderWithId();

        Assert.Contains("immutable", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindAll("button.btn-primary"));
    }

    [Fact]
    public void WhenEventDateInFuture_ShowsNotYetMessage_AndNoSaveButton()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event> { FutureEvent });

        var cut = RenderWithId();

        Assert.Contains("Participation can be recorded starting", cut.Markup);
        Assert.Empty(cut.FindAll("button.btn-primary"));
    }

    // --- Helpers ---

    private IRenderedComponent<ParticipationGrid> RenderWithId() =>
        Render<ParticipationGrid>(p => p.Add(x => x.EventId, EventId));

    private static Member ActiveMember(string name) => new()
    {
        Id = Guid.NewGuid(), FirstName = name, StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
