using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for the General settings tab's close-period control (spec 028, US6 / FR-016):
/// the closed-through date only reaches <see cref="ISettingsService.SaveAsync"/> when the
/// treasurer both picks a date and ticks the confirmation checkbox.
/// </summary>
public class ClosePeriodControlTests : LocalizedTestContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public ClosePeriodControlTests()
    {
        Services.AddSingleton(_settingsService);
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    private static AppSettings MakeSettings(DateTime? closedThrough = null) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        ClosedThroughDate = closedThrough,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void CloseThroughControls_Render()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        Assert.NotNull(cut.Find("#settings-close-through-date"));
        Assert.NotNull(cut.Find("#settings-close-through-confirm"));
    }

    [Fact]
    public async Task Save_SetsClosedThroughDate_When_DateEnteredAndConfirmed()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();
        cut.Find("#settings-close-through-date").Change("2025-12-31");
        cut.Find("#settings-close-through-confirm").Change(true);
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s!.ClosedThroughDate == new DateTime(2025, 12, 31)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_LeavesClosedThroughDateUnchanged_When_NotConfirmed()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();
        cut.Find("#settings-close-through-date").Change("2025-12-31"); // date picked but box left unticked
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s!.ClosedThroughDate == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CloseThroughDate_SeedsFromThePersistedValue()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings(new DateTime(2024, 6, 30)));

        var cut = Render<GeneralSettingsTab>();

        Assert.Equal("2024-06-30", cut.Find("#settings-close-through-date").GetAttribute("value"));
    }
}
