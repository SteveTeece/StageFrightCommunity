using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Events;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for EventForm — field rendering, validation, submission.
/// </summary>
public class EventFormTests : BunitContext
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();
    private readonly IEventTypeService _eventTypeService = Substitute.For<IEventTypeService>();

    private static readonly Guid PerformanceId = Guid.NewGuid();

    public EventFormTests()
    {
        Services.AddSingleton(_eventService);
        Services.AddSingleton(_eventTypeService);
        Services.AddSingleton(Substitute.For<Microsoft.AspNetCore.Components.NavigationManager>());

        _eventTypeService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<EventType>
            {
                new() { Id = PerformanceId, Name = "Performance", IsSystemDefault = true,
                         CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            });

        _eventService.ScheduleAsync(Arg.Any<ScheduleEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Event { Id = Guid.NewGuid(), EventTypeId = PerformanceId,
                                  Date = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    [Fact]
    public void Renders_DateField()
    {
        var cut = Render<EventForm>();
        cut.Find("#date");
    }

    [Fact]
    public void Renders_EventTypeSelect()
    {
        var cut = Render<EventForm>();
        cut.Find("#event-type");
    }

    [Fact]
    public void Renders_NotesField()
    {
        var cut = Render<EventForm>();
        cut.Find("#notes");
    }

    [Fact]
    public void DoesNotRender_FeeOrPaidFields()
    {
        var cut = Render<EventForm>();

        Assert.DoesNotContain("fee", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paid", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unpaid", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_ScheduleButton()
    {
        var cut = Render<EventForm>();
        var btn = cut.Find("button[type=submit]");
        Assert.Contains("Schedule", btn.TextContent);
    }

    [Fact]
    public void Renders_CancelButton()
    {
        var cut = Render<EventForm>();
        cut.Find("button.btn-outline-secondary");
    }

    [Fact]
    public void Renders_EventTypeOptions_FromService()
    {
        var cut = Render<EventForm>();
        Assert.Contains("Performance", cut.Markup);
    }
}
