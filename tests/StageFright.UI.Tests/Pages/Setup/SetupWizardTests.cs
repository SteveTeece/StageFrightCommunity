using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit component tests for the SetupWizard first-run page.
/// Verifies form rendering, client-side validation messages, and service call on valid submit.
/// </summary>
public class SetupWizardTests : BunitContext
{
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();

    public SetupWizardTests()
    {
        Services.AddSingleton(_setupService);
    }

    [Fact]
    public void Renders_FormWithRequiredFields()
    {
        var cut = Render<SetupWizard>();

        cut.Find("#orgName");
        cut.Find("#annualFee");
        cut.Find("#attendanceFee");
        cut.Find("#renewalMonth");
        cut.Find("[type=submit]");
    }

    [Fact]
    public async Task EmptyOrgName_ShowsRequiredValidationMessage()
    {
        var cut = Render<SetupWizard>();

        await cut.Find("form").SubmitAsync();

        Assert.Contains("required", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidSubmit_CallsInitializeAsyncWithCorrectValues()
    {
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<SetupWizard>();

        cut.Find("#orgName").Change("My Choir");
        cut.Find("#annualFee").Change("80");
        cut.Find("#attendanceFee").Change("5");

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.OrganizationName == "My Choir" && r.AnnualFee == 80m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceValidationException_ShowsErrorAlert()
    {
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Core.Exceptions.ValidationException(
                "Already set up.", "Settings", "InitializeAsync"));

        var cut = Render<SetupWizard>();

        cut.Find("#orgName").Change("Test Org");
        await cut.Find("form").SubmitAsync();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("Already set up.", alert.TextContent);
    }

    [Fact]
    public async Task ValidSubmit_NavigatesToDashboard()
    {
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<SetupWizard>();

        cut.Find("#orgName").Change("Test Org");
        await cut.Find("form").SubmitAsync();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
    }
}
