using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for AgmList — most-recent-first ordering, date + attendance count columns,
/// row click navigating to AGM detail (FR-015).
/// </summary>
public class AgmListTests : RadzenGridTestContext
{
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();

    private static readonly Guid OlderAgmId = Guid.NewGuid();
    private static readonly Guid NewerAgmId = Guid.NewGuid();

    public AgmListTests()
    {
        Services.AddSingleton(_agmService);
    }

    private static AnnualGeneralMeeting MakeAgm(Guid id, DateTime date, int attendedCount, int notAttendedCount)
    {
        var agm = new AnnualGeneralMeeting
        {
            Id = id, Date = date, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
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
        _agmService.GetPastAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { newer, older });

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
        _agmService.GetPastAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();

        Assert.Contains("15 March 2026", cut.Markup);
        Assert.Contains("3 of 4", cut.Markup);
    }

    [Fact]
    public void ClickingAgmDate_NavigatesToAgmDetail()
    {
        var agm = MakeAgm(NewerAgmId, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), 1, 0);
        _agmService.GetPastAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting> { agm });

        var cut = Render<AgmList>();
        cut.Find("a").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/events/agm/{NewerAgmId}", nav.Uri);
    }

    [Fact]
    public void NoAgms_ShowsEmptyState_WithRecordAgmLink()
    {
        _agmService.GetPastAsync(Arg.Any<CancellationToken>()).Returns(new List<AnnualGeneralMeeting>());

        var cut = Render<AgmList>();

        Assert.Contains("No AGMs have been recorded yet", cut.Markup);
        var link = cut.Find("a.btn-primary");
        Assert.Equal("/events/agm/new", link.GetAttribute("href"));
    }
}
