using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for EventList — rendering, empty state, navigation triggers.
/// </summary>
public class EventListTests : RadzenGridTestContext
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();

    private static readonly Guid EventTypeId = Guid.NewGuid();

    private static readonly EventType PerformanceType = new()
    {
        Id = EventTypeId,
        Name = "Performance",
        IsSystemDefault = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public EventListTests()
    {
        Services.AddSingleton(_eventService);
        Services.AddSingleton(Substitute.For<Microsoft.AspNetCore.Components.NavigationManager>());
    }

    [Fact]
    public void Renders_EmptyState_WhenNoEvents()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event>());

        var cut = Render<EventList>();

        Assert.Contains("No events scheduled yet", cut.Markup);
    }

    [Fact]
    public void Renders_ScheduleEventButton()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event>());

        var cut = Render<EventList>();

        cut.Find("button.btn-primary");
    }

    [Fact]
    public void Renders_EventList_WhenEventsExist()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event>
            {
                AnEvent("Summer Concert"),
                AnEvent("Eisteddfod Entry")
            });

        var cut = Render<EventList>();

        Assert.Contains("Not recorded", cut.Markup);
    }

    [Fact]
    public void Renders_ParticipationRecorded_WhenRateIsSet()
    {
        var evt = AnEvent("Winter Show");
        evt.StoredParticipationRate = 85m;

        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event> { evt });

        var cut = Render<EventList>();

        // When participation is recorded the Actions column shows "Recorded"
        // and the Participation column shows the rate (e.g. "85.0%").
        Assert.Contains("Recorded", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("85", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotShow_FeeColumns_InEventList()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Event> { AnEvent("Concert") });

        var cut = Render<EventList>();

        Assert.DoesNotContain("Fee", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Paid", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers ---

    private static Event AnEvent(string notes = "Test") => new()
    {
        Id = Guid.NewGuid(),
        Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EventTypeId = EventTypeId,
        EventType = PerformanceType,
        Notes = notes,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
