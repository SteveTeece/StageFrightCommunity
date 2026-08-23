using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Events;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Events;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for EventList — rendering, empty state, navigation triggers, and the Print
/// attendance sheet action (issue #302): button rendering and the empty-state/error alert paths
/// (the happy-path render→temp-file→launch is not click-tested — no seam exists to intercept the
/// real File.WriteAllBytes/Process.Start call, matching RehearsalListTests.cs's precedent).
/// </summary>
public class EventListTests : RadzenGridTestContext
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();
    private readonly IEventAttendanceSheetService _eventAttendanceSheetService = Substitute.For<IEventAttendanceSheetService>();
    private readonly IEventAttendanceSheetPdfRenderer _eventAttendanceSheetPdfRenderer = Substitute.For<IEventAttendanceSheetPdfRenderer>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

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
        Services.AddSingleton(_eventAttendanceSheetService);
        Services.AddSingleton(_eventAttendanceSheetPdfRenderer);
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

    // --- Print Attendance Sheet ---

    [Fact]
    public void PrintButton_Renders_ForEveryRow()
    {
        var evt = AnEvent("Concert");
        _eventService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Event> { evt });

        var cut = Render<EventList>();

        cut.Find($"button[aria-label='Print attendance sheet for {evt.Date:d MMM yyyy}']");
    }

    [Fact]
    public async Task ClickPrint_EmptyMembers_ShowsMessage_AndDoesNotRenderPdf()
    {
        var evt = AnEvent("Concert");
        _eventService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Event> { evt });
        _eventAttendanceSheetService.GenerateAsync(evt.Id, Arg.Any<CancellationToken>())
            .Returns(new EventAttendanceSheetData { EventDate = evt.Date, EventTypeName = "Performance", Members = Array.Empty<EventAttendanceSheetMember>() });

        var cut = Render<EventList>();
        await cut.Find($"button[aria-label='Print attendance sheet for {evt.Date:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("No active members found", cut.Markup);
        _eventAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<EventAttendanceSheetData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrint_ServiceThrows_ShowsErrorMessage_AndDoesNotRenderPdf()
    {
        var evt = AnEvent("Concert");
        _eventService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Event> { evt });
        _eventAttendanceSheetService.GenerateAsync(evt.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventAttendanceSheetData>(new EntityNotFoundException("Event", evt.Id, "GenerateAsync")));

        var cut = Render<EventList>();
        await cut.Find($"button[aria-label='Print attendance sheet for {evt.Date:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print", cut.Markup);
        _eventAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<EventAttendanceSheetData>(), Arg.Any<string>());
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
