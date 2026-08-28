using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for the theme dropdown on the Setup Wizard (issue #248, US6): the dropdown
/// reflects and controls the cascaded ThemeProvider's current theme, and whatever theme
/// is selected when the wizard is submitted flows into the SetupRequest.
/// </summary>
public class SetupWizardThemeTests : LocalizedTestContext
{
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();

    public SetupWizardThemeTests()
    {
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_openingBalanceService);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Core.Entities.Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Dark);
        _accountService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>());
        _accountService.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>());
        _openingBalanceService.GetOpeningBalanceAccountsAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>());
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.show", _ => true);
    }

    private IRenderedComponent<ThemeProvider> RenderWizard() =>
        Render<ThemeProvider>(p => p.AddChildContent<SetupWizard>());

    private static void AdvanceToReview(IRenderedComponent<ThemeProvider> cut)
    {
        cut.Find("#orgName").Change("My Choir");
        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Review
    }

    // No seeder is registered in this test class, so the Finish gate (FR-021) can only be
    // satisfied by a real queued balance — invoking OpeningBalancesTab's OnSubmit parameter
    // directly, same as SetupWizardNoSeederTests, since bUnit can't simulate a submit
    // through its nested <EditForm>.
    private static async Task QueueOpeningBalanceAsync(IRenderedComponent<ThemeProvider> cut)
    {
        var tab = cut.FindComponent<OpeningBalancesTab>();
        await cut.InvokeAsync(() => tab.Instance.OnSubmit.InvokeAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = DateTime.Today,
            Entries = new List<OpeningBalanceEntry> { new() { AccountId = Guid.NewGuid(), Amount = 100m } }
        }));
    }

    [Fact]
    public void ThemeDropdown_Renders_OnSetupWizard()
    {
        var cut = RenderWizard();

        cut.Find("#themeSelect");
    }

    [Fact]
    public void ThemeDropdown_DefaultsToDevicePreference()
    {
        var cut = RenderWizard();

        Assert.Equal(nameof(Theme.Dark), cut.Find("#themeSelect").GetAttribute("value"));
    }

    [Fact]
    public void ThemeDropdown_SelectingLight_ChangesDisplayedTheme()
    {
        var cut = RenderWizard();

        cut.Find("#themeSelect").Change(nameof(Theme.Light));

        Assert.Equal(nameof(Theme.Light), cut.Find("#themeSelect").GetAttribute("value"));
    }

    [Fact]
    public async Task Finish_IncludesDefaultTheme_InSetupRequest_WhenDropdownUntouched()
    {
        var cut = RenderWizard();
        AdvanceToReview(cut);
        await QueueOpeningBalanceAsync(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.Theme == Theme.Dark),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_IncludesSelectedTheme_InSetupRequest_WhenUserSwitchesToLight()
    {
        var cut = RenderWizard();
        cut.Find("#themeSelect").Change(nameof(Theme.Light));
        AdvanceToReview(cut);
        await QueueOpeningBalanceAsync(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.Theme == Theme.Light),
            Arg.Any<CancellationToken>());
    }
}
