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
public class ParticipationGridTests : BunitContext
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();

    private static readonly Guid EventId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid EventTypeId = Guid.NewGuid();

    private static readonly Event OpenEvent = new()
    {
        Id = EventId,
        Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EventTypeId = EventTypeId,
        EventType = new EventType { Id = EventTypeId, Name = "Performance" },
        StoredParticipationRate = null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static readonly Event LockedEvent = new()
    {
        Id = EventId,
        Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EventTypeId = EventTypeId,
        EventType = new EventType { Id = EventTypeId, Name = "Performance" },
        StoredParticipationRate = 75m,
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

    // --- Helpers ---

    private IRenderedComponent<ParticipationGrid> RenderWithId() =>
        Render<ParticipationGrid>(p => p.Add(x => x.EventId, EventId));

    private static Member ActiveMember(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
