using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.UI.Pages.Events;

namespace StageFright.UI.Tests.Pages.Events;

/// <summary>
/// bUnit tests for ScheduleAgm — date/notes-only form, ScheduleAsync save behavior,
/// ValidationException surfaced inline, and post-save redirect to the new AGM's detail page
/// (research.md Decision 7, not back to the list).
/// </summary>
public class ScheduleAgmTests : LocalizedTestContext
{
    private readonly IAgmService _agmService = Substitute.For<IAgmService>();

    private static readonly Guid SavedAgmId = Guid.NewGuid();

    public ScheduleAgmTests()
    {
        Services.AddSingleton(_agmService);
    }

    [Fact]
    public void Renders_DateField()
    {
        var cut = Render<ScheduleAgm>();

        cut.Find("#agmDate");
    }

    [Fact]
    public void Renders_NotesField()
    {
        var cut = Render<ScheduleAgm>();

        cut.Find("#agmNotes");
    }

    [Fact]
    public async Task Save_CallsScheduleAsync_WithEnteredDateAndNotes()
    {
        _agmService.ScheduleAsync(Arg.Any<ScheduleAgmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnnualGeneralMeeting
            {
                Id = SavedAgmId, Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var cut = Render<ScheduleAgm>();
        cut.Find("#agmDate").Change("2026-03-15");
        cut.Find("#agmNotes").Change("Annual sitting");

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _agmService.Received(1).ScheduleAsync(
            Arg.Is<ScheduleAgmRequest>(r =>
                r!.Date == new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Unspecified) &&
                r.Notes == "Annual sitting"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_ValidationException_ShowsMessageInline()
    {
        _agmService.ScheduleAsync(Arg.Any<ScheduleAgmRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AnnualGeneralMeeting>(
                new ValidationException("An AGM already exists for 2026.", nameof(AnnualGeneralMeeting), "ScheduleAsync")));

        var cut = Render<ScheduleAgm>();
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var alert = cut.Find(".alert-danger");
        Assert.Contains("An AGM already exists for 2026.", alert.TextContent);
    }

    [Fact]
    public async Task Save_Succeeds_NavigatesToAgmDetail_ForSavedAgm()
    {
        _agmService.ScheduleAsync(Arg.Any<ScheduleAgmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnnualGeneralMeeting
            {
                Id = SavedAgmId, Date = DateTime.Today,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var cut = Render<ScheduleAgm>();
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/events/agm/{SavedAgmId}", nav.Uri);
    }
}
