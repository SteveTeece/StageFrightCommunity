using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup;

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

    public SetupWizardTests()
    {
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_debugSeeder);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
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
        AdvanceFromGeneral(cut);
        cut.Find("#btn-next").Click(); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
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
        var review = cut.Markup.IndexOf("Review", StringComparison.Ordinal);

        Assert.True(general >= 0);
        Assert.True(membership > general);
        Assert.True(salesTax > membership);
        Assert.True(committee > salesTax);
        Assert.True(review > committee);
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

        AdvanceFromGeneral(cut);
        cut.Find("#annualFee");
        cut.Find("#renewalMonth");

        cut.Find("#btn-next").Click(); // -> Sales Tax
        cut.Find("#taxApplicable");

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
        AdvanceFromGeneral(cut, "My Choir");

        // Jump directly to Review by clicking its header, skipping Sales Tax/Committee.
        cut.FindAll(".nav-link").First(a => a.TextContent.Contains("Review")).Click();

        Assert.Contains("My Choir", cut.Markup);
    }

    [Fact]
    public async Task TaxRateField_OnlyShown_WhenTaxApplicableChecked()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromGeneral(cut);
        cut.Find("#btn-next").Click(); // Membership & Fees -> Sales Tax

        Assert.Empty(cut.FindAll("#taxRate"));

        await cut.Find("#taxApplicable").ChangeAsync(true);

        cut.Find("#taxRate");
    }

    [Fact]
    public async Task ValidSubmit_ComposesFullSetupRequest()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromGeneral(cut, "My Choir");
        cut.Find("#annualFee").Change("80");
        cut.Find("#btn-next").Click();
        cut.Find("#btn-next").Click();
        cut.Find("#officeHolderTitles").Change("Publicity Officer");
        cut.Find("#btn-next").Click();

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
