using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit component tests for the 5-step SetupWizard: step navigation/validation gating,
/// GST dropdown visibility, Finish composing the full SetupRequest, and the sample-data
/// seeding overlay appearing only once seeding actually starts.
/// </summary>
public class SetupWizardTests : BunitContext
{
    private const string ValidAbn = "51824753556";

    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly IDebugDataSeeder _debugSeeder = Substitute.For<IDebugDataSeeder>();

    public SetupWizardTests()
    {
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_debugSeeder);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private static void AdvanceFromStep1(IRenderedComponent<SetupWizard> cut, string orgName = "My Choir", string abn = ValidAbn)
    {
        cut.Find("#orgName").Change(orgName);
        cut.Find("#abn").Change(abn);
        cut.Find("#btn-next").Click();
    }

    private static void AdvanceFromStep2(IRenderedComponent<SetupWizard> cut)
    {
        cut.Find("#btn-next").Click();
    }

    private static void AdvanceFromStep3(IRenderedComponent<SetupWizard> cut)
    {
        cut.Find("#btn-next").Click();
    }

    private static void AdvanceFromStep4(IRenderedComponent<SetupWizard> cut)
    {
        cut.Find("#btn-next").Click();
    }

    [Fact]
    public void Step1_RendersOrganisationAndAbnFields()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#orgName");
        cut.Find("#abn");
        Assert.Contains("Step 1 of 5", cut.Markup);
    }

    [Fact]
    public void Next_Blocked_WhenOrgNameEmpty()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#abn").Change(ValidAbn);
        cut.Find("#btn-next").Click();

        Assert.Contains("Step 1 of 5", cut.Markup);
        Assert.Contains("required", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Next_Blocked_WhenAbnEmpty()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#orgName").Change("My Choir");
        cut.Find("#btn-next").Click();

        Assert.Contains("Step 1 of 5", cut.Markup);
    }

#if !DEBUG
    [Fact]
    public void Next_Blocked_WhenAbnInvalid()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#orgName").Change("My Choir");
        cut.Find("#abn").Change("12345");
        cut.Find("#btn-next").Click();

        Assert.Contains("Step 1 of 5", cut.Markup);
    }
#else
    [Fact]
    public void Next_AllowsMalformedAbn_InDebugBuild()
    {
        // ABN checksum validation is disabled in Debug builds (see SetupFormModel.Abn) so
        // developers can click through setup without a real, checksum-valid ABN.
        var cut = Render<SetupWizard>();

        cut.Find("#orgName").Change("My Choir");
        cut.Find("#abn").Change("12345");
        cut.Find("#btn-next").Click();

        Assert.Contains("Step 2 of 5", cut.Markup);
    }
#endif

    [Fact]
    public void Next_AdvancesThroughAllSteps_ToReview()
    {
        var cut = Render<SetupWizard>();

        AdvanceFromStep1(cut);
        Assert.Contains("Step 2 of 5", cut.Markup);
        cut.Find("#annualFee");
        cut.Find("#renewalMonth");

        AdvanceFromStep2(cut);
        Assert.Contains("Step 3 of 5", cut.Markup);
        cut.Find("#gstRegistered");

        AdvanceFromStep3(cut);
        Assert.Contains("Step 4 of 5", cut.Markup);
        cut.Find("#agmMonth");
        cut.Find("#officeHolderTitles");
        cut.Find("#seatCountTarget");

        AdvanceFromStep4(cut);
        Assert.Contains("Step 5 of 5", cut.Markup);
        cut.Find("#seedData");
        cut.Find("#btn-finish");
    }

    [Fact]
    public void Back_ReturnsToPreviousStep_WithValuesRetained()
    {
        var cut = Render<SetupWizard>();

        AdvanceFromStep1(cut, orgName: "Retained Org");
        cut.Find("#btn-back").Click();

        Assert.Contains("Step 1 of 5", cut.Markup);
        Assert.Equal("Retained Org", cut.Find("#orgName").GetAttribute("value"));
    }

    [Fact]
    public void GstDropdowns_AppearOnlyWhenToggledOn()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        AdvanceFromStep2(cut);

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeGstCode"));

        cut.Find("#gstRegistered").Change(true);
        cut.Find("#annualFeeGstCode");
        cut.Find("#attendanceFeeGstCode");

        cut.Find("#gstRegistered").Change(false);
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeGstCode"));
    }

    [Fact]
    public async Task Finish_ComposesFullSetupRequest_WhenGstRegistered()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut, orgName: "GST Org");
        AdvanceFromStep2(cut);

        cut.Find("#gstRegistered").Change(true);
        cut.Find("#annualFeeGstCode").Change("Gst");
        cut.Find("#attendanceFeeGstCode").Change("GstFree");
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r =>
                r.OrganizationName == "GST Org" &&
                r.Abn == ValidAbn &&
                r.IsGstRegistered &&
                r.AnnualFeeGstCode == Core.Enums.GstCode.Gst &&
                r.AttendanceFeeGstCode == Core.Enums.GstCode.GstFree),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Step2_RendersAuditRetentionDropdown_WithSevenOptions()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);

        var select = cut.Find("#auditRetentionYears");
        Assert.Equal(7, select.Children.Length);
    }

    [Fact]
    public async Task Finish_ComposesRequest_WithDefaultAuditRetentionYears_WhenUnchanged()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        AdvanceFromStep2(cut);
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.AuditRetentionYears == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_ComposesRequest_WithSelectedAuditRetentionYears()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        cut.Find("#auditRetentionYears").Change("5");
        AdvanceFromStep2(cut);
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.AuditRetentionYears == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_ForcesGstCodesNull_WhenNotRegistered()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        AdvanceFromStep2(cut);
        // GST left off (default) on step 3.
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => !r.IsGstRegistered && r.AnnualFeeGstCode == null && r.AttendanceFeeGstCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceValidationException_ShowsErrorAlert()
    {
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Core.Exceptions.ValidationException(
                "Already set up.", "Settings", "InitializeAsync"));

        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        AdvanceFromStep2(cut);
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);

        await cut.Find("form").SubmitAsync();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("Already set up.", alert.TextContent);
    }

    [Fact]
    public async Task ValidSubmit_NavigatesToDashboard_WhenNotSeeding()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        AdvanceFromStep2(cut);
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);

        await cut.Find("form").SubmitAsync();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
    }

    [Fact]
    public async Task SeedingOverlay_NeverAppears_WhenSampleDataNotRequested()
    {
        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        AdvanceFromStep2(cut);
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);

        await cut.Find("form").SubmitAsync();

        Assert.DoesNotContain("setup-seeding-overlay", cut.Markup);
        await _debugSeeder.DidNotReceive().SeedAsync(Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedingOverlay_AppearsOnlyOnceSeedingStarts_WhenSampleDataRequested()
    {
        var seedTcs = new TaskCompletionSource();
        _debugSeeder.SeedAsync(Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(seedTcs.Task);

        var cut = Render<SetupWizard>();
        AdvanceFromStep1(cut);
        AdvanceFromStep2(cut);
        AdvanceFromStep3(cut);
        AdvanceFromStep4(cut);
        cut.Find("#seedData").Change(true);

        Assert.DoesNotContain("setup-seeding-overlay", cut.Markup);

        var submitTask = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        await cut.WaitForStateAsync(() => cut.Markup.Contains("setup-seeding-overlay"));
        Assert.Contains("this may take a few minutes", cut.Markup);

        seedTcs.SetResult();
        await submitTask;

        Assert.DoesNotContain("setup-seeding-overlay", cut.Markup);
    }
}
