using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for the theme toggle switch on the Setup Wizard (issue #248): the switch
/// reflects and controls the cascaded ThemeProvider's current theme, and whatever theme
/// is selected when the wizard is submitted flows into the SetupRequest.
/// </summary>
public class SetupWizardThemeTests : BunitContext
{
    private const string ValidAbn = "51824753556";

    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();

    public SetupWizardThemeTests()
    {
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Core.Entities.Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Dark);
    }

    private IRenderedComponent<ThemeProvider> RenderWizard() =>
        Render<ThemeProvider>(p => p.AddChildContent<SetupWizard>());

    private static void AdvanceToReview(IRenderedComponent<ThemeProvider> cut)
    {
        cut.Find("#orgName").Change("My Choir");
        cut.Find("#abn").Change(ValidAbn);
        cut.Find("#btn-next").Click(); // -> step 2
        cut.Find("#btn-next").Click(); // -> step 3
        cut.Find("#btn-next").Click(); // -> step 4 (Review & Finish)
    }

    [Fact]
    public void ThemeToggle_Renders_OnSetupWizard()
    {
        var cut = RenderWizard();

        cut.Find(".setup-theme-toggle [role=switch]");
    }

    [Fact]
    public void ThemeToggle_DefaultsToDevicePreference()
    {
        var cut = RenderWizard();

        Assert.Contains("Dark", cut.Find(".setup-theme-toggle").TextContent);
    }

    [Fact]
    public void ThemeToggle_TogglingSwitch_ChangesDisplayedTheme()
    {
        var cut = RenderWizard();

        cut.Find(".setup-theme-toggle [role=switch]").Click();

        Assert.Contains("Light", cut.Find(".setup-theme-toggle").TextContent);
    }

    [Fact]
    public async Task Finish_IncludesDefaultTheme_InSetupRequest_WhenToggleUntouched()
    {
        var cut = RenderWizard();
        AdvanceToReview(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.Theme == Theme.Dark),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_IncludesToggledTheme_InSetupRequest_WhenUserSwitchesToLight()
    {
        var cut = RenderWizard();
        cut.Find(".setup-theme-toggle [role=switch]").Click(); // Dark -> Light
        AdvanceToReview(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.Theme == Theme.Light),
            Arg.Any<CancellationToken>());
    }
}
