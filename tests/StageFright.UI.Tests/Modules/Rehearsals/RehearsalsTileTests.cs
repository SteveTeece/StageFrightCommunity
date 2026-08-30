using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Modules.Rehearsals;

namespace StageFright.UI.Tests.Modules.Rehearsals;

/// <summary>
/// bUnit tests for RehearsalsTile (design 3a) — upcoming/next stats and the last
/// recorded attendance "n of m (x%)" with its progress bar, across all states.
/// </summary>
public class RehearsalsTileTests : LocalizedTestContext
{
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();
    private readonly IAttendanceRepository _attendanceRepository = Substitute.For<IAttendanceRepository>();

    public RehearsalsTileTests()
    {
        Services.AddSingleton(_rehearsalService);
        Services.AddSingleton(_attendanceRepository);
        SetupRecords(0, 0);
    }

    [Fact]
    public void Should_ShowLoading_When_DataNotYetLoaded()
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<IReadOnlyList<Rehearsal>>().Task);

        var cut = Render<RehearsalsTile>();

        Assert.Contains("Loading", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_ServiceThrows()
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<RehearsalsTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_RenderUpcomingCountAndNextDate_When_FutureRehearsalsExist()
    {
        var next = DateTime.Today.AddDays(3);
        SetupRehearsals(
            MakeRehearsal(DateTime.Today.AddDays(10)),
            MakeRehearsal(next),
            MakeRehearsal(DateTime.Today.AddDays(-7), rate: 80m));
        SetupLastRecorded(null);

        var cut = Render<RehearsalsTile>();

        var values = cut.FindAll(".tile-stat-value").Select(v => v.TextContent).ToList();
        Assert.Equal("2", values[0]);
        Assert.Equal(next.ToString("MMM d"), values[1]);
    }

    [Fact]
    public void Should_ShowDashForNext_When_NoUpcomingRehearsals()
    {
        SetupRehearsals();
        SetupLastRecorded(null);

        var cut = Render<RehearsalsTile>();

        var values = cut.FindAll(".tile-stat-value").Select(v => v.TextContent).ToList();
        Assert.Equal("0", values[0]);
        Assert.Equal("—", values[1]);
    }

    [Fact]
    public void Should_RenderCountsAndGoodBar_When_LastAttendanceIsEightyOrMore()
    {
        SetupRehearsals();
        var recorded = MakeRehearsal(DateTime.Today.AddDays(-2), rate: 81.8m);
        SetupLastRecorded(recorded);
        SetupRecords(attended: 18, absent: 4);

        var cut = Render<RehearsalsTile>();

        Assert.Contains("Last attendance", cut.Markup);
        Assert.Contains("18 of 22 (82%)", cut.Find(".tile-note").TextContent);
        var fill = cut.Find(".tile-progress-fill");
        Assert.Contains("good", fill.ClassList);
        Assert.Contains("width:82%", fill.GetAttribute("style")!.Replace(" ", ""));
    }

    [Fact]
    public void Should_UseAccentBar_When_LastAttendanceBelowEighty()
    {
        SetupRehearsals();
        SetupLastRecorded(MakeRehearsal(DateTime.Today.AddDays(-2), rate: 68.2m));
        SetupRecords(attended: 15, absent: 7);

        var cut = Render<RehearsalsTile>();

        Assert.DoesNotContain("good", cut.Find(".tile-progress-fill").ClassList);
    }

    [Fact]
    public void Should_ShowNotRecordedNote_When_NoAttendanceRecordedYet()
    {
        SetupRehearsals();
        SetupLastRecorded(null);

        var cut = Render<RehearsalsTile>();

        Assert.Contains("No attendance recorded yet", cut.Markup);
        Assert.Empty(cut.FindAll(".tile-progress"));
    }

    // --- Helpers ---

    private void SetupRehearsals(params Rehearsal[] rehearsals)
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Rehearsal>)rehearsals.ToList());
    }

    private void SetupLastRecorded(Rehearsal? rehearsal)
    {
        _rehearsalService.GetMostRecentPastWithAttendanceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(rehearsal);
    }

    private void SetupRecords(int attended, int absent)
    {
        var records = Enumerable.Range(0, attended)
            .Select(_ => new AttendanceRecord { Id = Guid.NewGuid(), Attended = true })
            .Concat(Enumerable.Range(0, absent)
                .Select(_ => new AttendanceRecord { Id = Guid.NewGuid(), Attended = false }))
            .ToList();
        _attendanceRepository.GetByRehearsalAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AttendanceRecord>)records);
    }

    private static Rehearsal MakeRehearsal(DateTime date, decimal? rate = null) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        StoredAttendanceRate = rate
    };
}
