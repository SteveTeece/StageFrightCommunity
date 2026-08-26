using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit component tests for the tabbed SetupWizard (spec 017, spec 022): tab-click
/// navigation, Next validation gating, Sales Tax dropdown visibility, Finish composing
/// the full SetupRequest, the sample-data seeding overlay, and — since spec 022 — the
/// relocated "Load sample data" checkbox's tab-bypass/queue-discard behavior.
/// </summary>
public class SetupWizardTests : BunitContext
{
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly IDebugDataSeeder _debugSeeder = Substitute.For<IDebugDataSeeder>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();

    public SetupWizardTests()
    {
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_debugSeeder);
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

    // spec 022: the checkbox now lives on Organisation Settings, and checking it before
    // ever clicking Next makes the single Next click land directly on Review (FR-004).
    private static async Task CheckSampleDataAndAdvanceToReviewAsync(IRenderedComponent<SetupWizard> cut, string orgName = "My Choir")
    {
        cut.Find("#orgName").Change(orgName);
        await cut.Find("#seedData").ChangeAsync(true);
        cut.Find("#btn-next").Click();
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

        var organisationSettings = cut.Markup.IndexOf("Organisation Settings", StringComparison.Ordinal);
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
        await CheckSampleDataAndAdvanceToReviewAsync(cut);

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
    public void SeedDataCheckbox_RendersOnOrganisationSettingsTab_WhenSeederAvailable()
    {
        var cut = Render<SetupWizard>();

        // Visible immediately on the first tab — no navigation needed (FR-001).
        cut.Find("#seedData");
    }

    [Fact]
    public async Task SeedingOverlay_AppearsOnlyOnceSeedingStarts()
    {
        var cut = Render<SetupWizard>();
        await CheckSampleDataAndAdvanceToReviewAsync(cut);

        Assert.DoesNotContain("setup-seeding-overlay", cut.Markup);

        await cut.Find("form").SubmitAsync();

        await _debugSeeder.Received(1).SeedAsync(Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());
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
        await CheckSampleDataAndAdvanceToReviewAsync(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.CommitteeOfficeHolderTitles!.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_Blocked_When_NoBalanceEntered_AndSampleDataUnselected()
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
    public async Task Finish_Succeeds_When_SampleDataSelected_AndNoBalanceEntered()
    {
        var cut = Render<SetupWizard>();
        await CheckSampleDataAndAdvanceToReviewAsync(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r!.QueuedOpeningBalances == null),
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

    // --- spec 022 US2: selecting sample data bypasses the three manual-entry tabs ---

    [Fact]
    public async Task ChartOfAccountsOpeningBalancesCommittee_BecomeDisabled_WhenSampleDataChecked()
    {
        var cut = Render<SetupWizard>();
        cut.Find("#orgName").Change("My Choir");
        await cut.Find("#seedData").ChangeAsync(true);

        // Checked at the component-parameter level, not via rendered CSS: BlazorBootstrap's
        // Tab applies its own "disabled" class through the same JS interop that drives
        // Bootstrap's tab.js, which these tests stub as a no-op (see the JSInterop.SetupVoid
        // calls in the constructor) — the same MAUI-WebView-JS-interop gap CLAUDE.md's Known
        // Gotchas documents for Radzen. The Disabled parameter itself — what this app's code
        // actually controls — is what's asserted here.
        var tabs = cut.FindComponents<BlazorBootstrap.Tab>();
        Assert.True(tabs.Single(t => t.Instance.Title == "Chart of Accounts").Instance.Disabled);
        Assert.True(tabs.Single(t => t.Instance.Title == "Opening Balances").Instance.Disabled);
        Assert.True(tabs.Single(t => t.Instance.Title == "Committee").Instance.Disabled);
        Assert.False(tabs.Single(t => t.Instance.Title == "Organisation Settings").Instance.Disabled);
        Assert.False(tabs.Single(t => t.Instance.Title == "Review").Instance.Disabled);
    }

    [Fact]
    public async Task ClickingBypassedTabHeader_DoesNotNavigate_WhenSampleDataChecked()
    {
        var cut = Render<SetupWizard>();
        cut.Find("#orgName").Change("My Choir");
        await cut.Find("#seedData").ChangeAsync(true);

        cut.FindAll(".nav-link").First(a => a.TextContent.Contains("Chart of Accounts")).Click();

        // Still on Organisation Settings — its field remains visible and Chart of
        // Accounts' content was never activated (FR-003).
        cut.Find("#orgName");
        Assert.Empty(cut.FindAll("#account-name"));
    }

    [Fact]
    public async Task Next_SkipsBypassedTabs_GoesStraightToReview_WhenSampleDataChecked()
    {
        var cut = Render<SetupWizard>();
        cut.Find("#orgName").Change("My Choir");
        await cut.Find("#seedData").ChangeAsync(true);

        cut.Find("#btn-next").Click();

        Assert.Contains("Review", cut.Markup);
        cut.Find("#btn-finish");
        Assert.Empty(cut.FindAll("#account-name"));
    }

    // --- spec 022 US3: toggling the checkbox keeps the wizard consistent ---

    [Fact]
    public async Task CheckingSampleData_DiscardsQueuedAccountBalanceAndCommitteeTitle()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromOrganisationSettings(cut, "My Choir"); // -> Chart of Accounts

        var coaTab = cut.FindComponent<ChartOfAccountsTab>();
        await cut.InvokeAsync(() => coaTab.Instance.OnAdd.InvokeAsync(
            new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, false)));

        cut.Find("#btn-next").Click(); // -> Opening Balances
        await QueueRealOpeningBalanceAsync(cut);

        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();

        // Navigate back to Organisation Settings (its header is never bypassed) and check
        // the box — this must discard everything just queued above (FR-006).
        cut.FindAll(".nav-link").First(a => a.TextContent.Contains("Organisation Settings")).Click();
        await cut.Find("#seedData").ChangeAsync(true);
        cut.Find("#btn-next").Click(); // -> Review directly (tabs now bypassed)

        Assert.Contains("None queued.", cut.Markup);
        Assert.Contains("None.", cut.Markup);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r!.QueuedAccounts == null
                && r.QueuedOpeningBalances == null
                && r.CommitteeOfficeHolderTitles!.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UncheckingSampleData_MakesTabsAvailableAgain_StartingEmpty()
    {
        var cut = Render<SetupWizard>();
        cut.Find("#orgName").Change("My Choir");
        await cut.Find("#seedData").ChangeAsync(true);
        await cut.Find("#seedData").ChangeAsync(false);

        cut.Find("#btn-next").Click(); // -> Chart of Accounts (no longer bypassed)

        cut.Find("#account-name"); // reachable and empty — no leftover queued account
    }

    [Fact]
    public async Task Finish_Blocked_AfterUncheckingSampleData_WithNoBalanceEntered()
    {
        var cut = Render<SetupWizard>();
        cut.Find("#orgName").Change("My Choir");
        await cut.Find("#seedData").ChangeAsync(true);
        await cut.Find("#seedData").ChangeAsync(false);

        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Review

        await cut.Find("form").SubmitAsync();

        Assert.Contains("opening balance", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _setupService.DidNotReceive().InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>());
    }
}
