using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Modules.Events;

namespace StageFright.UI.Tests.Modules.Events;

/// <summary>
/// bUnit tests for EventsTile (design 3a) — upcoming/next stats and the last recorded
/// participation "n of m (x%)" with its progress bar, across all states.
/// </summary>
public class EventsTileTests : LocalizedTestContext
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();

    public EventsTileTests()
    {
        Services.AddSingleton(_eventService);
    }

    [Fact]
    public void Should_ShowLoading_When_DataNotYetLoaded()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<IReadOnlyList<Event>>().Task);

        var cut = Render<EventsTile>();

        Assert.Contains("Loading", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_ServiceThrows()
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<EventsTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_RenderUpcomingCountAndNextDate_When_FutureEventsExist()
    {
        var next = DateTime.Today.AddDays(12);
        SetupEvents(
            MakeEvent(next),
            MakeEvent(DateTime.Today.AddDays(29)),
            MakeEvent(DateTime.Today.AddDays(-20), rate: 72.7m));
        SetupLastRecorded(null);

        var cut = Render<EventsTile>();

        var values = cut.FindAll(".tile-stat-value").Select(v => v.TextContent).ToList();
        Assert.Equal("2", values[0]);
        Assert.Equal(next.ToString("MMM d"), values[1]);
    }

    [Fact]
    public void Should_ShowDashForNext_When_NoUpcomingEvents()
    {
        SetupEvents();
        SetupLastRecorded(null);

        var cut = Render<EventsTile>();

        var values = cut.FindAll(".tile-stat-value").Select(v => v.TextContent).ToList();
        Assert.Equal("0", values[0]);
        Assert.Equal("—", values[1]);
    }

    [Fact]
    public void Should_RenderCountsAndAccentBar_When_LastParticipationBelowEighty()
    {
        SetupEvents();
        var recorded = MakeEvent(DateTime.Today.AddDays(-5), rate: 72.7m);
        SetupLastRecorded(recorded, participated: 16, absent: 6);

        var cut = Render<EventsTile>();

        Assert.Contains("Last participation", cut.Markup);
        Assert.Contains("16 of 22 (73%)", cut.Find(".tile-note").TextContent);
        var fill = cut.Find(".tile-progress-fill");
        Assert.DoesNotContain("good", fill.ClassList);
        Assert.Contains("width:73%", fill.GetAttribute("style")!.Replace(" ", ""));
    }

    [Fact]
    public void Should_UseGoodBar_When_LastParticipationEightyOrMore()
    {
        SetupEvents();
        SetupLastRecorded(MakeEvent(DateTime.Today.AddDays(-5), rate: 86.4m), participated: 19, absent: 3);

        var cut = Render<EventsTile>();

        Assert.Contains("good", cut.Find(".tile-progress-fill").ClassList);
    }

    [Fact]
    public void Should_ShowNotRecordedNote_When_NoParticipationRecordedYet()
    {
        SetupEvents();
        SetupLastRecorded(null);

        var cut = Render<EventsTile>();

        Assert.Contains("No participation recorded yet", cut.Markup);
        Assert.Empty(cut.FindAll(".tile-progress"));
    }

    // --- Helpers ---

    private void SetupEvents(params Event[] events)
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Event>)events.ToList());
    }

    private void SetupLastRecorded(Event? evt, int participated = 0, int absent = 0)
    {
        _eventService.GetMostRecentPastWithParticipationAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(evt);

        if (evt is not null)
        {
            var withRecords = MakeEvent(evt.Date, evt.StoredParticipationRate);
            withRecords.ParticipationRecords = Enumerable.Range(0, participated)
                .Select(_ => new ParticipationRecord { Id = Guid.NewGuid(), Participated = true })
                .Concat(Enumerable.Range(0, absent)
                    .Select(_ => new ParticipationRecord { Id = Guid.NewGuid(), Participated = false }))
                .ToList();
            _eventService.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>())
                .Returns(withRecords);
        }
    }

    private static Event MakeEvent(DateTime date, decimal? rate = null) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        StoredParticipationRate = rate
    };
}
