using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Rehearsals;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Rehearsals;

/// <summary>
/// bUnit tests for RehearsalList — future/past windowing (issue #233): only the next 3
/// future rehearsals are shown, all current-calendar-year past rehearsals are shown,
/// prior-calendar-year rehearsals are excluded, and the grid is sorted newest-first.
/// Also covers the Print Roll action (issue #257): button rendering and the empty-state/
/// error alert paths (the happy-path render→temp-file→launch is not click-tested — no seam
/// exists to intercept the real File.WriteAllBytes/Process.Start call).
/// </summary>
public class RehearsalListTests : LocalizedTestContext
{
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();
    private readonly IAttendanceRollService _attendanceRollService = Substitute.For<IAttendanceRollService>();
    private readonly IAttendanceRollPdfRenderer _attendanceRollPdfRenderer = Substitute.For<IAttendanceRollPdfRenderer>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly DateTime Today = DateTime.Today;

    public RehearsalListTests()
    {
        Services.AddSingleton(_rehearsalService);
        Services.AddSingleton(_attendanceRollService);
        Services.AddSingleton(_attendanceRollPdfRenderer);
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
    public void Renders_EmptyState_WhenNoRehearsals()
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Rehearsal>());

        var cut = Render<RehearsalList>();

        Assert.Contains("No rehearsals scheduled yet", cut.Markup);
    }

    [Fact]
    public void Shows_OnlyNextThree_OfManyFutureRehearsals()
    {
        var future = Enumerable.Range(1, 6)
            .Select(i => ARehearsal(Today.AddDays(i)))
            .ToList();

        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(future);

        var cut = Render<RehearsalList>();

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(3, rows.Count);
        Assert.Contains(Today.AddDays(1).ToString("d MMM yyyy"), cut.Markup);
        Assert.Contains(Today.AddDays(2).ToString("d MMM yyyy"), cut.Markup);
        Assert.Contains(Today.AddDays(3).ToString("d MMM yyyy"), cut.Markup);
        Assert.DoesNotContain(Today.AddDays(4).ToString("d MMM yyyy"), cut.Markup);
    }

    [Fact]
    public void Shows_TodayRehearsal_AsFuture()
    {
        var rehearsal = ARehearsal(Today);

        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Rehearsal> { rehearsal });

        var cut = Render<RehearsalList>();

        Assert.Contains(Today.ToString("d MMM yyyy"), cut.Markup);
    }

    [Fact]
    public void Shows_AllPastRehearsals_WithinCurrentCalendarYear()
    {
        var jan1ThisYear = new DateTime(Today.Year, 1, 1);
        var past = new List<Rehearsal>
        {
            ARehearsal(jan1ThisYear),
            ARehearsal(Today.AddDays(-1))
        };

        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(past);

        var cut = Render<RehearsalList>();

        Assert.Contains(jan1ThisYear.ToString("d MMM yyyy"), cut.Markup);
        Assert.Contains(Today.AddDays(-1).ToString("d MMM yyyy"), cut.Markup);
    }

    [Fact]
    public void Excludes_PastRehearsals_FromPriorCalendarYears()
    {
        var lastYear = new DateTime(Today.Year - 1, 12, 31);
        var thisYear = new DateTime(Today.Year, 1, 1);

        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Rehearsal> { ARehearsal(lastYear), ARehearsal(thisYear) });

        var cut = Render<RehearsalList>();

        Assert.DoesNotContain(lastYear.ToString("d MMM yyyy"), cut.Markup);
        Assert.Contains(thisYear.ToString("d MMM yyyy"), cut.Markup);
    }

    [Fact]
    public void Sorts_NewestFirst_OldestLast()
    {
        var older = ARehearsal(Today.AddDays(-2));
        var newer = ARehearsal(Today.AddDays(1));

        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Rehearsal> { older, newer });

        var cut = Render<RehearsalList>();

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains(newer.Date.ToString("d MMM yyyy"), rows[0].TextContent);
        Assert.Contains(older.Date.ToString("d MMM yyyy"), rows[1].TextContent);
    }

    // --- Print Roll ---

    [Fact]
    public void PrintRollButton_Renders_ForEveryRow()
    {
        var rehearsal = ARehearsal(Today);
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Rehearsal> { rehearsal });

        var cut = Render<RehearsalList>();

        cut.Find($"button[aria-label='Print roll for {Today:d MMM yyyy}']");
    }

    [Fact]
    public async Task ClickPrintRoll_EmptyMembers_ShowsAlert_AndDoesNotRenderPdf()
    {
        var rehearsal = ARehearsal(Today);
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Rehearsal> { rehearsal });
        _attendanceRollService.GenerateAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new AttendanceRollData { RehearsalDate = rehearsal.Date, RehearsalTime = rehearsal.Time, Members = Array.Empty<AttendanceRollMember>() });

        var cut = Render<RehearsalList>();
        await cut.Find($"button[aria-label='Print roll for {Today:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("No active members found", cut.Markup);
        _attendanceRollPdfRenderer.DidNotReceive().Render(Arg.Any<AttendanceRollData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrintRoll_ServiceThrows_ShowsErrorAlert_AndDoesNotRenderPdf()
    {
        var rehearsal = ARehearsal(Today);
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Rehearsal> { rehearsal });
        _attendanceRollService.GenerateAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AttendanceRollData>(new EntityNotFoundException("Rehearsal", rehearsal.Id, "GenerateAsync")));

        var cut = Render<RehearsalList>();
        await cut.Find($"button[aria-label='Print roll for {Today:d MMM yyyy}']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Unable to print roll", cut.Markup);
        _attendanceRollPdfRenderer.DidNotReceive().Render(Arg.Any<AttendanceRollData>(), Arg.Any<string>());
    }

    // --- Helpers ---

    private static Rehearsal ARehearsal(DateTime date) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        Time = TimeSpan.FromHours(19),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
