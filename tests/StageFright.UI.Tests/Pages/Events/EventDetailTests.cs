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
/// bUnit tests for EventDetail — read-only detail rendering plus the Print Attendance Sheet
/// action (issue #302). This page had no test file before this feature.
/// </summary>
public class EventDetailTests : LocalizedTestContext
{
    private readonly IEventService _eventService = Substitute.For<IEventService>();
    private readonly IEventAttendanceSheetService _eventAttendanceSheetService = Substitute.For<IEventAttendanceSheetService>();
    private readonly IEventAttendanceSheetPdfRenderer _eventAttendanceSheetPdfRenderer = Substitute.For<IEventAttendanceSheetPdfRenderer>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid EventTypeId = Guid.NewGuid();

    private static readonly EventType PerformanceType = new()
    {
        Id = EventTypeId,
        Name = "Performance",
        IsSystemDefault = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public EventDetailTests()
    {
        Services.AddSingleton(_eventService);
        Services.AddSingleton(_eventAttendanceSheetService);
        Services.AddSingleton(_eventAttendanceSheetPdfRenderer);
        Services.AddSingleton(_settingsService);

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

    private static Event AnEvent() => new()
    {
        Id = EventId,
        Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EventTypeId = EventTypeId,
        EventType = PerformanceType,
        ParticipationRecords = new List<ParticipationRecord>(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void Renders_EventDetails_ForValidId()
    {
        _eventService.GetByIdWithDetailsAsync(EventId, Arg.Any<CancellationToken>()).Returns(AnEvent());

        var cut = Render<EventDetail>(p => p.Add(x => x.Id, EventId));

        Assert.Contains("Performance", cut.Markup);
    }

    [Fact]
    public void Shows_EventNotFound_ForUnknownId()
    {
        _eventService.GetByIdWithDetailsAsync(EventId, Arg.Any<CancellationToken>()).Returns((Event?)null);

        var cut = Render<EventDetail>(p => p.Add(x => x.Id, EventId));

        Assert.Contains("Event not found.", cut.Markup);
    }

    [Fact]
    public void PrintButton_Renders_OnceEventLoads()
    {
        _eventService.GetByIdWithDetailsAsync(EventId, Arg.Any<CancellationToken>()).Returns(AnEvent());

        var cut = Render<EventDetail>(p => p.Add(x => x.Id, EventId));

        cut.Find("button[aria-label='Print attendance sheet']");
    }

    [Fact]
    public async Task ClickPrint_EmptyMembers_ShowsMessage_AndDoesNotRenderPdf()
    {
        _eventService.GetByIdWithDetailsAsync(EventId, Arg.Any<CancellationToken>()).Returns(AnEvent());
        _eventAttendanceSheetService.GenerateAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(new EventAttendanceSheetData { EventDate = AnEvent().Date, EventTypeName = "Performance", Members = Array.Empty<EventAttendanceSheetMember>() });

        var cut = Render<EventDetail>(p => p.Add(x => x.Id, EventId));
        await cut.Find("button[aria-label='Print attendance sheet']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("No active members found", cut.Markup);
        _eventAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<EventAttendanceSheetData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrint_ServiceThrows_ShowsErrorMessage_AndDoesNotRenderPdf()
    {
        _eventService.GetByIdWithDetailsAsync(EventId, Arg.Any<CancellationToken>()).Returns(AnEvent());
        _eventAttendanceSheetService.GenerateAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventAttendanceSheetData>(new EntityNotFoundException("Event", EventId, "GenerateAsync")));

        var cut = Render<EventDetail>(p => p.Add(x => x.Id, EventId));
        await cut.Find("button[aria-label='Print attendance sheet']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print", cut.Markup);
        _eventAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<EventAttendanceSheetData>(), Arg.Any<string>());
    }
}
