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
/// bUnit component tests for the tabbed SetupWizard (spec 017): tab-click navigation,
/// Next validation gating, Sales Tax dropdown visibility, Finish composing the full
/// SetupRequest, and the sample-data seeding overlay appearing only once seeding starts.
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

    private static void AdvanceFromGeneral(IRenderedComponent<SetupWizard> cut, string orgName = "My Choir")
    {
        cut.Find("#orgName").Change(orgName);
        cut.Find("#btn-next").Click();
    }

    private static void AdvanceToReview(IRenderedComponent<SetupWizard> cut)
    {
        AdvanceFromGeneral(cut); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Review
    }

    [Fact]
    public void AllTabs_RenderInDefinedOrder_OnFirstLoad()
    {
        var cut = Render<SetupWizard>();

        var general = cut.Markup.IndexOf("General", StringComparison.Ordinal);
        var membership = cut.Markup.IndexOf("Membership", StringComparison.Ordinal);
        var salesTax = cut.Markup.IndexOf("Sales Tax", StringComparison.Ordinal);
        var committee = cut.Markup.IndexOf("Committee", StringComparison.Ordinal);
        var chartOfAccounts = cut.Markup.IndexOf("Chart of Accounts", StringComparison.Ordinal);
        var openingBalances = cut.Markup.IndexOf("Opening Balances", StringComparison.Ordinal);
        var review = cut.Markup.IndexOf("Review", StringComparison.Ordinal);

        Assert.True(general >= 0);
        Assert.True(membership > general);
        Assert.True(salesTax > membership);
        Assert.True(committee > salesTax);
        Assert.True(chartOfAccounts > committee);
        Assert.True(openingBalances > chartOfAccounts);
        Assert.True(review > openingBalances);
    }

    [Fact]
    public void GeneralTab_RendersOrganisationField_WithNoAbnField()
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

        // Still on the General tab — the field and its validation message remain visible.
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

        cut.Find("#btn-next").Click(); // -> Sales Tax
        cut.Find("#taxApplicable");

        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#agmMonth");

        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#account-name");

        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#ob-tab-as-at-date");

        cut.Find("#btn-next").Click(); // -> Review
        Assert.Contains("Review", cut.Markup);
        cut.Find("#btn-finish");
    }

    [Fact]
    public void DirectTabClick_NavigatesWithoutLosingEnteredValues()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromGeneral(cut, "My Choir");

        // Jump directly to Review by clicking its header, skipping Sales Tax/Committee.
        cut.FindAll(".nav-link").First(a => a.TextContent.Contains("Review")).Click();

        Assert.Contains("My Choir", cut.Markup);
    }

    [Fact]
    public async Task TaxRateField_OnlyShown_WhenTaxApplicableChecked()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromGeneral(cut); // -> Sales Tax

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
        cut.Find("#btn-next").Click(); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Review
        // No opening balance entered — check the sample-data checkbox to satisfy the
        // Finish gate (FR-021); this test cares about SetupRequest composition, not the
        // opening-balance gate itself (covered separately in the US2 test suite).
        await cut.Find("#seedData").ChangeAsync(true);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r.OrganizationName == "My Choir"
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
        AdvanceFromGeneral(cut, "My Choir"); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Chart of Accounts

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
        cut.Find("#btn-next").Click(); // -> Review
        // No opening balance entered — satisfy the Finish gate (FR-021) via sample data;
        // this test is only about QueuedAccounts composition.
        await cut.Find("#seedData").ChangeAsync(true);
        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r.QueuedAccounts != null
                && r.QueuedAccounts.Count == 1
                && r.QueuedAccounts[0].Name == "Petty Cash"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_FinishesNormally_When_NoAccountsQueued()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);
        // No opening balance entered — satisfy the Finish gate (FR-021) via sample data.
        await cut.Find("#seedData").ChangeAsync(true);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.QueuedAccounts == null),
            Arg.Any<CancellationToken>());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
    }

    [Fact]
    public async Task RemovingQueuedAccount_OnChartOfAccountsTab_ExcludesItFromFinish()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromGeneral(cut, "My Choir"); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Chart of Accounts

        // See ValidSubmit_ComposesQueuedAccounts_When_AccountQueuedOnChartOfAccountsTab for
        // why this invokes the child's OnAdd/OnRemove parameters directly rather than
        // simulating a nested-form DOM submit.
        var tab = cut.FindComponent<ChartOfAccountsTab>();
        var queued = new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, false);
        await cut.InvokeAsync(() => tab.Instance.OnAdd.InvokeAsync(queued));
        tab = cut.FindComponent<ChartOfAccountsTab>();
        await cut.InvokeAsync(() => tab.Instance.OnRemove.InvokeAsync(queued));

        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Review
        // No opening balance entered — satisfy the Finish gate (FR-021) via sample data.
        await cut.Find("#seedData").ChangeAsync(true);
        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.QueuedAccounts == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewTab_ShowsEveryTabsEnteredValue_WithoutNavigatingBack()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromGeneral(cut, "My Choir"); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#btn-next").Click(); // -> Chart of Accounts

        var coaTab = cut.FindComponent<ChartOfAccountsTab>();
        await cut.InvokeAsync(() => coaTab.Instance.OnAdd.InvokeAsync(
            new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, false)));

        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Review

        Assert.Contains("My Choir", cut.Markup);
        Assert.Contains("Publicity Officer", cut.Markup);
        Assert.Contains("Petty Cash", cut.Markup);
    }

    [Fact]
    public void SeedDataCheckbox_RendersOnReviewTab_WhenSeederAvailable()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);

        cut.Find("#seedData");
    }

    [Fact]
    public async Task SeedingOverlay_AppearsOnlyOnceSeedingStarts()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);
        await cut.Find("#seedData").ChangeAsync(true);

        Assert.DoesNotContain("setup-seeding-overlay", cut.Markup);

        await cut.Find("form").SubmitAsync();

        await _debugSeeder.Received(1).SeedAsync(Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_ComposesQueuedCommitteeTitles_When_TitlesAdded()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromGeneral(cut, "My Choir"); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#committee-role-input").Input("Webmaster");
        cut.Find("#committee-role-add-btn").Click();
        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
        cut.Find("#btn-next").Click(); // -> Review
        await cut.Find("#seedData").ChangeAsync(true);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r.CommitteeOfficeHolderTitles!.Count == 2
                && r.CommitteeOfficeHolderTitles.Contains("Publicity Officer")
                && r.CommitteeOfficeHolderTitles.Contains("Webmaster")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_FinishesWithNoTitles_When_QueueLeftEmpty()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);
        await cut.Find("#seedData").ChangeAsync(true);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.CommitteeOfficeHolderTitles!.Count == 0),
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
                r.QueuedOpeningBalances != null
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
        AdvanceToReview(cut);
        await cut.Find("#seedData").ChangeAsync(true);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.QueuedOpeningBalances == null),
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
}
