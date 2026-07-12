using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Pages.Rehearsals;

namespace StageFright.UI.Tests.Pages.Rehearsals;

/// <summary>
/// bUnit tests for RehearsalList — future/past windowing (issue #233): only the next 3
/// future rehearsals are shown, all current-calendar-year past rehearsals are shown,
/// prior-calendar-year rehearsals are excluded, and the grid is sorted newest-first.
/// </summary>
public class RehearsalListTests : BunitContext
{
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();

    private static readonly DateTime Today = DateTime.Today;

    public RehearsalListTests()
    {
        Services.AddSingleton(_rehearsalService);
        Services.AddSingleton(Substitute.For<Microsoft.AspNetCore.Components.NavigationManager>());
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
