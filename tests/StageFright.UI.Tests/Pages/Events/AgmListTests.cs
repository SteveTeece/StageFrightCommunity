using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Events;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for AgmList — most-recent-first ordering, date/status/attendance columns, row
/// click navigating to AGM detail, the per-row Record action for scheduled AGMs (FR-003), and
/// the Print attendance report action (issue #302).
/// </summary>
public class AgmListTests : RadzenGridTestContext
{
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();
    private readonly IAgmAttendanceSheetService _agmAttendanceSheetService = Substitute.For<IAgmAttendanceSheetService>();
    private readonly IAgmAttendanceSheetPdfRenderer _agmAttendanceSheetPdfRenderer = Substitute.For<IAgmAttendanceSheetPdfRenderer>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Guid OlderAgmId = Guid.NewGuid();
    private static readonly Guid NewerAgmId = Guid.NewGuid();

    public AgmListTests()
    {
        Services.AddSingleton(_agmService);
        Services.AddSingleton(_agmAttendanceSheetService);
        Services.AddSingleton(_agmAttendanceSheetPdfRenderer);
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

    private static AnnualGeneralMeeting MakeAgm(Guid id, DateTime date, int attendedCount, int notAttendedCount, bool isRecorded = true)
    {
        var agm = new AnnualGeneralMeeting
        {
            Id = id, Date = date, IsRecorded = isRecorded, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        for (var i = 0; i < attendedCount; i++)
            agm.AttendanceRecords.Add(new AgmAttendanceRecord { Id = Guid.NewGuid(), AnnualGeneralMeetingId = id, MemberId = Guid.NewGuid(), Attended = true, CreatedAt = DateTime.UtcNow });
        for (var i = 0; i < notAttendedCount; i++)
            agm.AttendanceRecords.Add(new AgmAttendanceRecord { Id = Guid.NewGuid(), AnnualGeneralMeetingId = id, MemberId = Guid.NewGuid(), Attended = false, CreatedAt = DateTime.UtcNow });
        return agm;
    }

    [Fact]
    public void Renders_AgmsMostRecentFirst()
    {
        var older = MakeAgm(OlderAgmId, new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), 3, 1);
        var newer = MakeAgm(NewerAgmId, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 4, 0);
        // Service already returns most-recent-first (GetPastOrderedAsync); the list renders exactly what it's given.
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { newer, older });

        var cut = Render<AgmList>();

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("2026", rows[0].TextContent);
        Assert.Contains("2025", rows[1].TextContent);
    }

    [Fact]
    public void Renders_DateAndAttendanceCount_Columns()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 3, 1);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        Assert.Contains("15 March 2026", cut.Markup);
        Assert.Contains("3 of 4", cut.Markup);
    }

    [Fact]
    public void ClickingAgmDate_NavigatesToAgmDetail()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 1, 0);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();
        cut.Find("a").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/events/agm/{NewerAgmId}", nav.Uri);
    }

    [Fact]
    public void NoAgms_ShowsEmptyState_WithScheduleAgmLink()
    {
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting>());

        var cut = Render<AgmList>();

        Assert.Contains("No AGMs have been recorded yet", cut.Markup);
        var link = cut.Find("a.btn-primary");
        Assert.Equal("/events/agm/new", link.GetAttribute("href"));
    }

    // --- Status column (FR-003) ---

    [Fact]
    public void StatusColumn_ShowsRecorded_ForRecordedAgm()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 1, 0, isRecorded: true);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        Assert.Contains("Recorded", cut.Markup);
    }

    [Fact]
    public void StatusColumn_ShowsScheduled_ForScheduledAgm()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 0, 0, isRecorded: false);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        Assert.Contains("Scheduled", cut.Markup);
    }

    [Fact]
    public void AttendanceColumn_ShowsEmDash_ForScheduledAgm()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 0, 0, isRecorded: false);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        Assert.Contains("—", cut.Markup);
    }

    // --- Record action (FR-004) ---

    [Fact]
    public void RecordAction_Renders_ForScheduledAgm_AndNavigatesToRecordRoute()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 0, 0, isRecorded: false);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        var link = cut.Find("a.btn-primary");
        Assert.Equal($"/events/agm/{NewerAgmId}/record", link.GetAttribute("href"));
    }

    [Fact]
    public void RecordAction_DoesNotRender_ForRecordedAgm()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 1, 0, isRecorded: true);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        Assert.Empty(cut.FindAll("a.btn-primary"));
    }

    // --- Print Attendance Report ---

    [Fact]
    public void PrintButton_Renders_ForEveryRow()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 1, 0);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        cut.Find($"button[aria-label='Print attendance report for {agm.Date:d MMMM yyyy}']");
    }

    [Fact]
    public async Task ClickPrint_EmptyMembers_ShowsMessage_AndDoesNotRenderPdf()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 0, 0);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });
        _agmAttendanceSheetService.GenerateAsync(agm.Id, Arg.Any<CancellationToken>())
            .Returns(new AgmAttendanceSheetData { AgmDate = agm.Date, Members = Array.Empty<AgmAttendanceSheetMember>() });

        var cut = Render<AgmList>();
        await cut.Find($"button[aria-label='Print attendance report for {agm.Date:d MMMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("No attendance records found", cut.Markup);
        _agmAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<AgmAttendanceSheetData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrint_ServiceThrows_ShowsErrorMessage_AndDoesNotRenderPdf()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 1, 0);
        _agmService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });
        _agmAttendanceSheetService.GenerateAsync(agm.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AgmAttendanceSheetData>(new EntityNotFoundException("AnnualGeneralMeeting", agm.Id, "GenerateAsync")));

        var cut = Render<AgmList>();
        await cut.Find($"button[aria-label='Print attendance report for {agm.Date:d MMMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print", cut.Markup);
        _agmAttendanceSheetPdfRenderer.DidNotReceive().Render(Arg.Any<AgmAttendanceSheetData>(), Arg.Any<string>());
    }
}
