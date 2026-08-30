using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Modules.Rehearsals;

namespace StageFright.UI.Tests.Modules.Rehearsals;

/// <summary>
/// bUnit tests for AttendanceTrendTile (design 3a) — six-month attendance line chart
/// across loading, error, empty-window and populated states.
/// </summary>
public class AttendanceTrendTileTests : LocalizedTestContext
{
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();

    public AttendanceTrendTileTests()
    {
        Services.AddSingleton(_rehearsalService);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Should_ShowLoading_When_DataNotYetLoaded()
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<IReadOnlyList<Rehearsal>>().Task);

        var cut = Render<AttendanceTrendTile>();

        Assert.Contains("Loading", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_ServiceThrows()
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<AttendanceTrendTile>();

        Assert.Contains("Unable to load", cut.Markup);
    }

    [Fact]
    public void Should_ShowEmptyMessage_When_NoRecordedRehearsalsExist()
    {
        SetupRehearsals();

        var cut = Render<AttendanceTrendTile>();

        Assert.Contains("No attendance recorded in the last 6 months", cut.Markup);
    }

    [Fact]
    public void Should_ShowEmptyMessage_When_RecordedRehearsalsAreOlderThanWindow()
    {
        SetupRehearsals(MakeRehearsal(DateTime.Today.AddMonths(-8), rate: 75m));

        var cut = Render<AttendanceTrendTile>();

        Assert.Contains("No attendance recorded in the last 6 months", cut.Markup);
    }

    [Fact]
    public void Should_IgnoreUnrecordedAndFutureRehearsals_When_BuildingTrend()
    {
        SetupRehearsals(
            MakeRehearsal(DateTime.Today.AddDays(-10), rate: null),
            MakeRehearsal(DateTime.Today.AddDays(5), rate: 90m));

        var cut = Render<AttendanceTrendTile>();

        Assert.Contains("No attendance recorded in the last 6 months", cut.Markup);
    }

    [Fact]
    public void Should_RenderChart_When_RecordedRehearsalsInWindowExist()
    {
        SetupRehearsals(
            MakeRehearsal(DateTime.Today.AddDays(-10), rate: 82m),
            MakeRehearsal(DateTime.Today.AddMonths(-2), rate: 68m));

        var cut = Render<AttendanceTrendTile>();

        Assert.DoesNotContain("No attendance recorded", cut.Markup);
        Assert.NotEmpty(cut.FindAll("canvas"));
    }

    // --- Helpers ---

    private void SetupRehearsals(params Rehearsal[] rehearsals)
    {
        _rehearsalService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Rehearsal>)rehearsals.ToList());
    }

    private static Rehearsal MakeRehearsal(DateTime date, decimal? rate) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        StoredAttendanceRate = rate
    };
}
