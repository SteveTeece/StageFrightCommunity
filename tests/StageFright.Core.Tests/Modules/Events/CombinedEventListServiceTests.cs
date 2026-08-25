using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Events;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Events;

/// <summary>
/// Unit tests for CombinedEventListService — merging IEventService + IAgmService results into
/// one date-sorted list, per-kind field mapping, and (safety-critical) AGM-vs-event routing.
/// </summary>
public class CombinedEventListServiceTests : TestBase
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();

    private CombinedEventListService CreateService() => new(_eventService, _agmService);

    private static readonly Guid EventTypeId = Guid.NewGuid();

    private static EventType APerformanceType() => new()
    {
        Id = EventTypeId,
        Name = "Performance",
        IsSystemDefault = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Event AnEvent(DateTime date, decimal? participationRate = null, string? notes = null) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        EventTypeId = EventTypeId,
        EventType = APerformanceType(),
        Notes = notes,
        StoredParticipationRate = participationRate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static AnnualGeneralMeeting AnAgm(DateTime date, bool isRecorded = false, string? notes = null) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        Notes = notes,
        IsRecorded = isRecorded,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private void SetUpSources(IReadOnlyList<Event>? events = null, IReadOnlyList<AnnualGeneralMeeting>? agms = null)
    {
        _eventService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(events ?? []);
        _agmService.GetPastAsync(Arg.Any<CancellationToken>()).Returns(agms ?? []);
    }

    [Fact]
    public async Task GetAllAsync_Merges_EventAndAgmResults()
    {
        var evt = AnEvent(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var agm = AnAgm(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        SetUpSources(events: [evt], agms: [agm]);

        var result = await CreateService().GetAllAsync(Ct);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Id == evt.Id && i.Kind == CombinedEventListItemKind.Event);
        Assert.Contains(result, i => i.Id == agm.Id && i.Kind == CombinedEventListItemKind.Agm);
    }

    [Fact]
    public async Task GetAllAsync_Sorts_MergedListByDateDescending()
    {
        var oldest = AnEvent(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var middle = AnAgm(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newest = AnEvent(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetUpSources(events: [oldest, newest], agms: [middle]);

        var result = await CreateService().GetAllAsync(Ct);

        Assert.Equal(newest.Id, result[0].Id);
        Assert.Equal(middle.Id, result[1].Id);
        Assert.Equal(oldest.Id, result[2].Id);
    }

    [Fact]
    public async Task GetAllAsync_Maps_EventRow_WithTypeNameParticipationRateAndDetailUrl()
    {
        var evt = AnEvent(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), participationRate: 85.0m, notes: "Great show");
        SetUpSources(events: [evt]);

        var result = await CreateService().GetAllAsync(Ct);

        var item = Assert.Single(result);
        Assert.Equal(evt.Id, item.Id);
        Assert.Equal(evt.Date, item.Date);
        Assert.Equal("Great show", item.Notes);
        Assert.Equal("Performance", item.TypeName);
        Assert.Equal(CombinedEventListItemKind.Event, item.Kind);
        Assert.Equal(85.0m, item.ParticipationRate);
        Assert.Null(item.IsAgmRecorded);
        Assert.Equal($"/events/{evt.Id}", item.DetailUrl);
    }

    [Fact]
    public async Task GetAllAsync_Maps_AgmRow_WithFixedTypeNameRecordedFlagAndDetailUrl()
    {
        var agm = AnAgm(new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), isRecorded: true, notes: "Elections held");
        SetUpSources(agms: [agm]);

        var result = await CreateService().GetAllAsync(Ct);

        var item = Assert.Single(result);
        Assert.Equal(agm.Id, item.Id);
        Assert.Equal(agm.Date, item.Date);
        Assert.Equal("Elections held", item.Notes);
        Assert.Equal("Annual General Meeting", item.TypeName);
        Assert.Equal(CombinedEventListItemKind.Agm, item.Kind);
        Assert.Null(item.ParticipationRate);
        Assert.True(item.IsAgmRecorded);
        Assert.Equal($"/events/agm/{agm.Id}", item.DetailUrl);

        // FR-006 safety-critical routing: an AGM row must never resolve to the generic event route.
        Assert.NotEqual($"/events/{agm.Id}", item.DetailUrl);
    }

    [Fact]
    public async Task GetAllAsync_Returns_EmptyList_WhenBothSourcesEmpty()
    {
        SetUpSources();

        var result = await CreateService().GetAllAsync(Ct);

        Assert.Empty(result);
    }
}
