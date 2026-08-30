using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Modules.Events;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Events;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for EventList — rendering the combined Event+AGM list (spec 023 issue #320), empty
/// state, navigation triggers, and the Print attendance sheet action (issue #302): button rendering
/// and the empty-state/error alert paths (the happy-path render→temp-file→launch is not
/// click-tested — no seam exists to intercept the real File.WriteAllBytes/Process.Start call,
/// matching RehearsalListTests.cs's precedent).
/// </summary>
public class EventListTests : LocalizedTestContext
{
    private readonly ICombinedEventListService _combinedEventListService = Substitute.For<ICombinedEventListService>();
    private readonly IEventAttendanceSheetService _eventAttendanceSheetService = Substitute.For<IEventAttendanceSheetService>();
    private readonly IEventAttendanceSheetPdfRenderer _eventAttendanceSheetPdfRenderer = Substitute.For<IEventAttendanceSheetPdfRenderer>();
    private readonly IAgmAttendanceSheetService _agmAttendanceSheetService = Substitute.For<IAgmAttendanceSheetService>();
    private readonly IAgmAttendanceSheetPdfRenderer _agmAttendanceSheetPdfRenderer = Substitute.For<IAgmAttendanceSheetPdfRenderer>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public EventListTests()
    {
        Services.AddSingleton(_combinedEventListService);
        Services.AddSingleton(_eventAttendanceSheetService);
        Services.AddSingleton(_eventAttendanceSheetPdfRenderer);
        Services.AddSingleton(_agmAttendanceSheetService);
        Services.AddSingleton(_agmAttendanceSheetPdfRenderer);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(Substitute.For<Microsoft.AspNetCore.Components.NavigationManager>());

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SettingsEntity
            {
                Id = Guid.NewGuid(), OrganizationName = "Test Choir",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    private void SetUpItems(params CombinedEventListItem[] items) =>
        _combinedEventListService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(items.ToList());

    [Fact]
    public void Renders_EmptyState_WhenNoEvents()
    {
        SetUpItems();

        var cut = Render<EventList>();

        Assert.Contains("No events scheduled yet", cut.Markup);
    }

    [Fact]
    public void Renders_ScheduleEventButton()
    {
        SetUpItems();

        var cut = Render<EventList>();

        cut.Find("button.btn-primary");
    }

    [Fact]
    public void Renders_EventList_WhenEventsExist()
    {
        SetUpItems(AnEventItem("Summer Concert"), AnEventItem("Eisteddfod Entry"));

        var cut = Render<EventList>();

        Assert.Contains("Not recorded", cut.Markup);
    }

    [Fact]
    public void Renders_ParticipationRecorded_WhenRateIsSet()
    {
        var item = AnEventItem("Winter Show", participationRate: 85m);

        SetUpItems(item);

        var cut = Render<EventList>();

        // When participation is recorded the Actions column shows "Recorded"
        // and the Participation column shows the rate (e.g. "85.0%").
        Assert.Contains("Recorded", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("85", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotShow_FeeColumns_InEventList()
    {
        SetUpItems(AnEventItem("Concert"));

        var cut = Render<EventList>();

        Assert.DoesNotContain("Fee", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Paid", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- US1: AGMs appear in the combined All Events list ---

    [Fact]
    public void Renders_AgmAndEventRows_InSameGrid_OrderedByDateDescending()
    {
        // The service contract already guarantees Date-descending order (FR-002); EventList
        // renders whatever order it receives, so the newer row is seeded first here.
        var newerAgm = AnAgmItem(isRecorded: true, date: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var olderEvent = AnEventItem("Older Concert", date: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetUpItems(newerAgm, olderEvent);

        var cut = Render<EventList>();

        Assert.Contains("Annual General Meeting", cut.Markup);
        Assert.Contains("Older Concert", cut.Markup);
        var agmIndex = cut.Markup.IndexOf("Annual General Meeting", StringComparison.Ordinal);
        var eventIndex = cut.Markup.IndexOf("Older Concert", StringComparison.Ordinal);
        Assert.True(agmIndex < eventIndex, "Expected the more recent AGM row to render before the older Event row.");
    }

    [Fact]
    public void Renders_ScheduledButUnrecordedAgm_AlongsidePastEvents()
    {
        var scheduledAgm = AnAgmItem(isRecorded: false, date: new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var pastEvent = AnEventItem("Past Concert", date: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetUpItems(scheduledAgm, pastEvent);

        var cut = Render<EventList>();

        Assert.Contains("Annual General Meeting", cut.Markup);
        Assert.Contains("Past Concert", cut.Markup);
    }

    [Fact]
    public void Renders_EmptyState_WhenNoEventsAndNoAgms()
    {
        SetUpItems();

        var cut = Render<EventList>();

        Assert.Contains("No events scheduled yet", cut.Markup);
    }

    // --- US2: AGM rows read distinctly and act on the AGM's own pipeline ---

    [Fact]
    public void RecordedAgmRow_Renders_RecordedBadge_NeverParticipationPercentage()
    {
        var item = AnAgmItem(isRecorded: true);
        SetUpItems(item);

        var cut = Render<EventList>();

        var badge = cut.Find("span.badge.bg-success");
        Assert.Equal("Recorded", badge.TextContent.Trim());
        Assert.DoesNotContain('%', cut.Markup);
    }

    [Fact]
    public void ScheduledAgmRow_Renders_ScheduledBadge_AndRecordAction()
    {
        var item = AnAgmItem(isRecorded: false);
        SetUpItems(item);

        var cut = Render<EventList>();

        var badge = cut.Find("span.badge.bg-warning.text-dark");
        Assert.Equal("Scheduled", badge.TextContent.Trim());
        var recordLink = cut.Find($"a[href='/events/agm/{item.Id}/record']");
        Assert.Contains("Record", recordLink.TextContent);
    }

    [Fact]
    public async Task ClickPrint_OnAgmRow_UsesAgmAttendancePipeline_NotEventPipeline()
    {
        var item = AnAgmItem(isRecorded: true);
        SetUpItems(item);
        _agmAttendanceSheetService.GenerateAsync(item.Id, Arg.Any<CancellationToken>())
            .Returns(new AgmAttendanceSheetData { AgmDate = item.Date, Members = Array.Empty<AgmAttendanceSheetMember>() });

        var cut = Render<EventList>();
        await cut.Find($"button[aria-label='Print attendance report for {item.Date:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _agmAttendanceSheetService.Received(1).GenerateAsync(item.Id, Arg.Any<CancellationToken>());
        await _eventAttendanceSheetService.DidNotReceive().GenerateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClickPrint_OnAgmRow_ServiceThrows_ShowsErrorMessage_AndDoesNotRenderPdf()
    {
        var item = AnAgmItem(isRecorded: true);
        SetUpItems(item);
        _agmAttendanceSheetService.GenerateAsync(item.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AgmAttendanceSheetData>(new EntityNotFoundException("Agm", item.Id, "GenerateAsync")));

        var cut = Render<EventList>();
        await cut.Find($"button[aria-label='Print attendance report for {item.Date:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print", cut.Markup);
        _agmAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<AgmAttendanceSheetData>(), Arg.Any<string>());
    }

    [Fact]
    public void EventRow_DateLinkHref_IsGenericEventRoute_NeverAgmRoute()
    {
        var item = AnEventItem("Concert");
        SetUpItems(item);

        var cut = Render<EventList>();

        var link = cut.Find($"a[href='/events/{item.Id}']");
        Assert.NotNull(link);
    }

    [Fact]
    public void AgmRow_DateLinkHref_IsAgmDetailRoute_NeverGenericEventRoute()
    {
        var item = AnAgmItem(isRecorded: true);
        SetUpItems(item);

        var cut = Render<EventList>();

        var link = cut.Find($"a[href='/events/agm/{item.Id}']");
        Assert.NotNull(link);
        Assert.Empty(cut.FindAll($"a[href='/events/{item.Id}']"));
    }

    // --- US3: search finds AGM rows the same way it finds Event rows ---

    [Fact]
    public void Search_ByAgmDate_LeavesOnlyTheAgmRow()
    {
        var agm = AnAgmItem(isRecorded: true, date: new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));
        var evt = AnEventItem("Summer Concert", date: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        SetUpItems(agm, evt);

        var cut = Render<EventList>();
        cut.Find("input[type=search], input[placeholder*='earch' i]").Input(agm.Date.ToString("d MMM yyyy"));

        Assert.Contains("Annual General Meeting", cut.Markup);
        Assert.DoesNotContain("Summer Concert", cut.Markup);
    }

    [Fact]
    public void Search_ByAnnualGeneralMeetingTypeName_LeavesAgmRows_FiltersOutEventRows()
    {
        var agm = AnAgmItem(isRecorded: true);
        var evt = AnEventItem("Summer Concert");
        SetUpItems(agm, evt);

        var cut = Render<EventList>();
        cut.Find("input[type=search], input[placeholder*='earch' i]").Input("annual general meeting");

        Assert.Contains("Annual General Meeting", cut.Markup);
        Assert.DoesNotContain("Summer Concert", cut.Markup);
    }

    [Fact]
    public void Search_ByAgmNotes_LeavesOnlyThatAgmRow()
    {
        var agm = AnAgmItem(isRecorded: true, notes: "Elections held");
        var evt = AnEventItem("Summer Concert");
        SetUpItems(agm, evt);

        var cut = Render<EventList>();
        cut.Find("input[type=search], input[placeholder*='earch' i]").Input("Elections held");

        Assert.Contains("Annual General Meeting", cut.Markup);
        Assert.DoesNotContain("Summer Concert", cut.Markup);
    }

    [Fact]
    public void Search_MatchingNeitherAgmNorEvent_ShowsNoMatchesMessage()
    {
        var agm = AnAgmItem(isRecorded: true, notes: "Elections held");
        var evt = AnEventItem("Summer Concert");
        SetUpItems(agm, evt);

        var cut = Render<EventList>();
        cut.Find("input[type=search], input[placeholder*='earch' i]").Input("Nonexistent Term");

        Assert.Contains("No events match", cut.Markup);
        Assert.DoesNotContain("Annual General Meeting", cut.Markup);
        Assert.DoesNotContain("Summer Concert", cut.Markup);
    }

    // --- Print Attendance Sheet ---

    [Fact]
    public void PrintButton_Renders_ForEveryRow()
    {
        var item = AnEventItem("Concert");
        SetUpItems(item);

        var cut = Render<EventList>();

        cut.Find($"button[aria-label='Print attendance sheet for {item.Date:d MMM yyyy}']");
    }

    [Fact]
    public async Task ClickPrint_EmptyMembers_ShowsMessage_AndDoesNotRenderPdf()
    {
        var item = AnEventItem("Concert");
        SetUpItems(item);
        _eventAttendanceSheetService.GenerateAsync(item.Id, Arg.Any<CancellationToken>())
            .Returns(new EventAttendanceSheetData { EventDate = item.Date, EventTypeName = "Performance", Members = Array.Empty<EventAttendanceSheetMember>() });

        var cut = Render<EventList>();
        await cut.Find($"button[aria-label='Print attendance sheet for {item.Date:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("No active members found", cut.Markup);
        _eventAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<EventAttendanceSheetData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrint_ServiceThrows_ShowsErrorMessage_AndDoesNotRenderPdf()
    {
        var item = AnEventItem("Concert");
        SetUpItems(item);
        _eventAttendanceSheetService.GenerateAsync(item.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventAttendanceSheetData>(new EntityNotFoundException("Event", item.Id, "GenerateAsync")));

        var cut = Render<EventList>();
        await cut.Find($"button[aria-label='Print attendance sheet for {item.Date:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print", cut.Markup);
        _eventAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<EventAttendanceSheetData>(), Arg.Any<string>());
    }

    // --- Helpers ---

    private static CombinedEventListItem AnEventItem(string notes = "Test", decimal? participationRate = null, DateTime? date = null)
    {
        var id = Guid.NewGuid();
        return new CombinedEventListItem
        {
            Id = id,
            Date = date ?? new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Notes = notes,
            TypeName = "Performance",
            Kind = CombinedEventListItemKind.Event,
            ParticipationRate = participationRate,
            IsAgmRecorded = null,
            DetailUrl = $"/events/{id}"
        };
    }

    private static CombinedEventListItem AnAgmItem(bool isRecorded = false, DateTime? date = null, string? notes = null)
    {
        var id = Guid.NewGuid();
        return new CombinedEventListItem
        {
            Id = id,
            Date = date ?? new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc),
            Notes = notes,
            TypeName = "Annual General Meeting",
            Kind = CombinedEventListItemKind.Agm,
            ParticipationRate = null,
            IsAgmRecorded = isRecorded,
            DetailUrl = $"/events/agm/{id}"
        };
    }
}
