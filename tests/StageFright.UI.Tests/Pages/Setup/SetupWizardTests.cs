using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit component tests for the tabbed SetupWizard (spec 017, spec 022; spec 029 dropped the
/// language step/sample-data option and the tab-bypass mechanism outright): tab-click
/// navigation, Next validation gating, Sales Tax dropdown visibility, and Finish composing the
/// full SetupRequest — including LanguageCode now sourced from the cascaded CultureProvider
/// rather than a wizard field.
/// </summary>
public class SetupWizardTests : LocalizedTestContext
{
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();

    public SetupWizardTests()
    {
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_openingBalanceService);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _accountService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>());
        _accountService.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>());
        _openingBalanceService.GetOpeningBalanceAccountsAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>());
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.show", _ => true);
    }

    private IRenderedComponent<CultureProvider> RenderWizardUnderCulture() =>
        Render<CultureProvider>(p => p.AddChildContent<SetupWizard>());

    /// <summary>
    /// The localized "Organisation Settings" tab title, resolved through the same
    /// <see cref="IStringLocalizer{SetupResource}"/> the wizard renders it with — so these
    /// assertions hold under any UI culture (en-US on CI renders "Organization Settings").
    /// </summary>
    private string OrgSettingsTabTitle =>
        Services.GetRequiredService<IStringLocalizer<SetupResource>>()["Setup_Tab_OrganisationSettings"];

    private static void AdvanceFromOrganisationSettings(IRenderedComponent<SetupWizard> cut, string orgName = "My Choir")
    {
        cut.Find("#orgName").Change(orgName);
        cut.Find("#btn-next").Click();
    }

    private static void AdvanceToReview(IRenderedComponent<SetupWizard> cut)
    {
        AdvanceFromOrganisationSettings(cut); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Review
    }

    private static async Task QueueRealOpeningBalanceAsync(IRenderedComponent<SetupWizard> cut)
    {
        var obTab = cut.FindComponent<OpeningBalancesTab>();
        await cut.InvokeAsync(() => obTab.Instance.OnSubmit.InvokeAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = DateTime.Today,
            Entries = new List<OpeningBalanceEntry> { new() { AccountId = Guid.NewGuid(), Amount = 100m } }
        }));
    }

    [Fact]
    public void AllTabs_RenderInDefinedOrder_OnFirstLoad()
    {
        var cut = Render<SetupWizard>();

        var organisationSettings = cut.Markup.IndexOf(OrgSettingsTabTitle, StringComparison.Ordinal);
        var chartOfAccounts = cut.Markup.IndexOf("Chart of Accounts", StringComparison.Ordinal);
        var openingBalances = cut.Markup.IndexOf("Opening Balances", StringComparison.Ordinal);
        var committee = cut.Markup.IndexOf("Committee", StringComparison.Ordinal);
        var review = cut.Markup.IndexOf("Review", StringComparison.Ordinal);

        Assert.True(organisationSettings >= 0);
        Assert.True(chartOfAccounts > organisationSettings);
        Assert.True(openingBalances > chartOfAccounts);
        Assert.True(committee > openingBalances);
        Assert.True(review > committee);
    }

    [Fact]
    public void OrganisationSettingsTab_RendersOrganisationField_WithNoAbnField()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#orgName");
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#abn"));
    }

    [Fact]
    public void Next_Blocked_WhenOrgNameEmpty()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#btn-next").Click();

        // Still on the Organisation Settings tab — the field and its validation message
        // remain visible.
        cut.Find("#orgName");
        Assert.Contains("required", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Next_AdvancesThroughAllTabs_ToReview()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#orgName").Change("My Choir");
        cut.Find("#annualFee");
        cut.Find("#renewalMonth");
        cut.Find("#taxApplicable");

        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#account-name");

        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#ob-tab-as-at-date");

        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#agmMonth");

        cut.Find("#btn-next").Click(); // -> Review
        Assert.Contains("Review", cut.Markup);
        cut.Find("#btn-finish");
    }

    [Fact]
    public void DirectTabClick_NavigatesWithoutLosingEnteredValues()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromOrganisationSettings(cut, "My Choir");

        // Jump directly to Review by clicking its header, skipping Opening Balances/Committee.
        cut.FindAll(".nav-link").First(a => a.TextContent.Contains("Review")).Click();

        Assert.Contains("My Choir", cut.Markup);
    }

    [Fact]
    public async Task TaxRateField_OnlyShown_WhenTaxApplicableChecked()
    {
        var cut = Render<SetupWizard>();

        // Sales Tax fields now live on the same Organisation Settings tab as orgName —
        // no Next click needed to reach #taxApplicable.
        Assert.Empty(cut.FindAll("#taxRate"));

        await cut.Find("#taxApplicable").ChangeAsync(true);

        cut.Find("#taxRate");
    }

    [Fact]
    public async Task ValidSubmit_ComposesFullSetupRequest()
    {
        var cut = Render<SetupWizard>();
        cut.Find("#orgName").Change("My Choir");
        cut.Find("#annualFee").Change("80");
        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        // A real opening balance satisfies the Finish gate (FR-021) without checking
        // sample data — checking it would discard the committee title queued below
        // (FR-006), which this test needs intact to assert full request composition.
        await QueueRealOpeningBalanceAsync(cut);
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#btn-next").Click(); // -> Review

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r!.OrganizationName == "My Choir"
                && r.AnnualFee == 80m
                && r.CommitteeOfficeHolderTitles!.Contains("Publicity Officer")),
            Arg.Any<CancellationToken>());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
    }

    [Fact]
    public async Task ValidSubmit_ComposesQueuedAccounts_When_AccountQueuedOnChartOfAccountsTab()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromOrganisationSettings(cut, "My Choir"); // -> Chart of Accounts

        // ChartOfAccountsTab hosts AddAccountForm's own <EditForm>, nested inside the
        // wizard's outer one — real nested <form> elements resolve fine in a live browser
        // (submit-button targets its nearest form ancestor), but bUnit's AngleSharp-parsed
        // DOM collapses a nested <form> start tag, so its OnAdd callback can't be reached
        // by simulating a DOM form submit here. Invoking the child's own OnAdd parameter
        // directly exercises exactly what this test cares about — SetupWizard's wiring
        // between the tab's queue and Finish — without depending on that DOM quirk.
        var tab = cut.FindComponent<ChartOfAccountsTab>();
        var newAccount = new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, false);
        await cut.InvokeAsync(() => tab.Instance.OnAdd.InvokeAsync(newAccount));

        cut.Find("#btn-next").Click(); // -> Opening Balances
        // A real opening balance satisfies the Finish gate (FR-021) — checking the
        // sample-data box instead would discard the queued account this test asserts on
        // (FR-006).
        await QueueRealOpeningBalanceAsync(cut);
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Review
        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r!.QueuedAccounts != null
                && r.QueuedAccounts.Count == 1
                && r.QueuedAccounts[0].Name == "Petty Cash"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_FinishesNormally_When_NoAccountsQueued()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);
        await QueueRealOpeningBalanceAsync(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.QueuedAccounts == null),
            Arg.Any<CancellationToken>());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
    }

    [Fact]
    public async Task RemovingQueuedAccount_OnChartOfAccountsTab_ExcludesItFromFinish()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromOrganisationSettings(cut, "My Choir"); // -> Chart of Accounts

        // See ValidSubmit_ComposesQueuedAccounts_When_AccountQueuedOnChartOfAccountsTab for
        // why this invokes the child's OnAdd/OnRemove parameters directly rather than
        // simulating a nested-form DOM submit.
        var tab = cut.FindComponent<ChartOfAccountsTab>();
        var queued = new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, false);
        await cut.InvokeAsync(() => tab.Instance.OnAdd.InvokeAsync(queued));
        tab = cut.FindComponent<ChartOfAccountsTab>();
        await cut.InvokeAsync(() => tab.Instance.OnRemove.InvokeAsync(queued));

        cut.Find("#btn-next").Click(); // -> Opening Balances
        await QueueRealOpeningBalanceAsync(cut);
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Review
        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.QueuedAccounts == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewTab_ShowsEveryTabsEnteredValue_WithoutNavigatingBack()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromOrganisationSettings(cut, "My Choir"); // -> Chart of Accounts

        var coaTab = cut.FindComponent<ChartOfAccountsTab>();
        await cut.InvokeAsync(() => coaTab.Instance.OnAdd.InvokeAsync(
            new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, false)));

        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#btn-next").Click(); // -> Review

        Assert.Contains("My Choir", cut.Markup);
        Assert.Contains("Publicity Officer", cut.Markup);
        Assert.Contains("Petty Cash", cut.Markup);
    }

    [Fact]
    public void SeedDataCheckbox_IsAbsent_FromOrganisationSettingsTab()
    {
        // The debug-only "Load sample data" control moved to /language-select entirely
        // (spec 029 FR-014) — the wizard itself never resolves IDebugDataSeeder or renders it.
        var cut = Render<SetupWizard>();

        Assert.Empty(cut.FindAll("#seedData"));
    }

    [Fact]
    public void Tabs_AreNeverDisabled()
    {
        // The tab-bypass mechanism is gone outright (spec 029 FR-017) — every tab's Disabled
        // parameter stays false regardless of anything entered on Organisation Settings.
        var cut = Render<SetupWizard>();

        foreach (var tab in cut.FindComponents<BlazorBootstrap.Tab>())
        {
            Assert.False(tab.Instance.Disabled);
        }
    }

    [Fact]
    public async Task ValidSubmit_ComposesQueuedCommitteeTitles_When_TitlesAdded()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromOrganisationSettings(cut, "My Choir"); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        // A real opening balance satisfies Finish (FR-021) without discarding the titles
        // queued below — checking sample data would (FR-006).
        await QueueRealOpeningBalanceAsync(cut);
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#committee-role-input").Input("Webmaster");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#btn-next").Click(); // -> Review

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r!.CommitteeOfficeHolderTitles!.Count == 2
                && r.CommitteeOfficeHolderTitles.Contains("Publicity Officer")
                && r.CommitteeOfficeHolderTitles.Contains("Webmaster")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_FinishesWithNoTitles_When_QueueLeftEmpty()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);
        await QueueRealOpeningBalanceAsync(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.CommitteeOfficeHolderTitles!.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_Blocked_When_NoBalanceEntered()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);

        await cut.Find("form").SubmitAsync();

        Assert.Contains("opening balance", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _setupService.DidNotReceive().InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_Succeeds_When_BalanceEntered()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);

        var tab = cut.FindComponent<OpeningBalancesTab>();
        var asAtDate = new DateTime(2025, 7, 1);
        var accountId = Guid.NewGuid();
        await cut.InvokeAsync(() => tab.Instance.OnSubmit.InvokeAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = asAtDate,
            Entries = new List<OpeningBalanceEntry> { new() { AccountId = accountId, Amount = 500m } }
        }));

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r!.QueuedOpeningBalances != null
                && r.QueuedOpeningBalances.Count == 1
                && r.QueuedOpeningBalances[0].AccountId == accountId
                && r.QueuedOpeningBalances[0].Amount == 500m
                && r.OpeningBalanceAsAtDate == asAtDate),
            Arg.Any<CancellationToken>());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
    }

    [Fact]
    public async Task InvalidFinish_ShowsBannerError_WhenAnEarlierTabIsInvalid()
    {
        // Blank the organisation name via reflection-free means: enter it, advance, then
        // clear it before returning to Review isn't directly reachable through Next-only
        // navigation, so this exercises the same guard via a fresh render landing straight
        // on Review with the required field still blank (simulating a direct-tab-click skip).
        var cut = Render<SetupWizard>();
        cut.FindAll(".nav-link").First(a => a.TextContent.Contains("Review")).Click();

        await cut.Find("form").SubmitAsync();

        Assert.Contains("check every tab", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _setupService.DidNotReceive().InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_ComposesLanguageCode_FromCascadedCultureProvider()
    {
        var cut = RenderWizardUnderCulture();
        var wizard = cut.FindComponent<SetupWizard>();
        AdvanceFromOrganisationSettings(wizard, "My Choir"); // -> Chart of Accounts
        wizard.Find("#btn-next").Click(); // -> Opening Balances
        await QueueRealOpeningBalanceAsync(wizard);
        wizard.Find("#btn-next").Click(); // -> Committee
        wizard.Find("#btn-next").Click(); // -> Review
        await wizard.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.LanguageCode == cut.Instance.CurrentCulture.Name),
            Arg.Any<CancellationToken>());
    }
}
