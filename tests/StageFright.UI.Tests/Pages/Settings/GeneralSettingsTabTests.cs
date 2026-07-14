using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for GeneralSettingsTab after the GST/BAS split (FR-117): GST controls are
/// gone, the ABN field (with a non-blocking "not on file" notice) replaces them, and
/// HandleSaveAsync merges GST-owned fields from a fresh fetch before saving (FR-119).
/// </summary>
public class GeneralSettingsTabTests : BunitContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public GeneralSettingsTabTests()
    {
        Services.AddSingleton(_settingsService);
    }

    private static AppSettings MakeSettings(string? abn = "51824753556") => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        Abn = abn,
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void ThemeSwitch_IsUnchecked_WhenThemeIsLight()
    {
        var settings = MakeSettings();
        settings.Theme = Theme.Light;
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var cut = Render<GeneralSettingsTab>();

        var themeSwitch = cut.Find("#theme-toggle-switch");
        Assert.Equal("false", themeSwitch.GetAttribute("aria-checked"));
    }

    [Fact]
    public void ThemeSwitch_IsChecked_WhenThemeIsDark()
    {
        var settings = MakeSettings();
        settings.Theme = Theme.Dark;
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var cut = Render<GeneralSettingsTab>();

        var themeSwitch = cut.Find("#theme-toggle-switch");
        Assert.Equal("true", themeSwitch.GetAttribute("aria-checked"));
    }

    [Fact]
    public void GstControls_AreAbsent()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#gstRegistered"));
        Assert.DoesNotContain("GST / BAS", cut.Markup);
    }

    [Fact]
    public void AbnField_IsPresent()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        cut.Find("#abn");
    }

    [Fact]
    public void AbnNotOnFileNotice_Shows_WhenAbnEmpty()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings(abn: null));

        var cut = Render<GeneralSettingsTab>();

        Assert.Contains("ABN not on file", cut.Markup);
    }

    [Fact]
    public void AbnNotOnFileNotice_Hidden_WhenAbnPresent()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        Assert.DoesNotContain("ABN not on file", cut.Markup);
    }

    [Fact]
    public async Task Save_Succeeds_WithEmptyAbn()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings(abn: null));
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        Assert.Contains("Settings saved successfully", cut.Markup);
    }

    [Fact]
    public async Task HandleSaveAsync_MergesGstFields_FromFreshFetch()
    {
        var loaded = MakeSettings();
        var freshFromDb = MakeSettings();
        freshFromDb.IsGstRegistered = true;
        freshFromDb.AnnualFeeGstCode = GstCode.Gst;
        freshFromDb.AttendanceFeeGstCode = GstCode.GstFree;

        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(loaded, freshFromDb);
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s =>
                s.IsGstRegistered &&
                s.AnnualFeeGstCode == GstCode.Gst &&
                s.AttendanceFeeGstCode == GstCode.GstFree),
            Arg.Any<CancellationToken>());
    }
}
