using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for SetupWizard when IDebugDataSeeder is not registered — the state MauiProgram
/// leaves DI in for Release builds. There is never a database seed in Release, so the "Load
/// sample data" checkbox and its progress overlay must not render, and Finish must complete
/// without attempting to resolve or invoke the seeder.
/// </summary>
public class SetupWizardNoSeederTests : BunitContext
{
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();

    public SetupWizardNoSeederTests()
    {
        Services.AddSingleton(_setupService);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.show", _ => true);
    }

    private static void AdvanceToReview(IRenderedComponent<SetupWizard> cut)
    {
        cut.Find("#orgName").Change("My Choir");
        cut.Find("#btn-next").Click(); // -> Membership & Fees
        cut.Find("#btn-next").Click(); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Review
    }

    [Fact]
    public void SeedDataCheckbox_IsAbsent_WhenSeederNotRegistered()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);

        Assert.Contains("Review", cut.Markup);
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#seedData"));
    }

    [Fact]
    public async Task ValidSubmit_CompletesAndNavigates_WithoutSeeder()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);

        await cut.Find("form").SubmitAsync();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
        Assert.DoesNotContain("setup-seeding-overlay", cut.Markup);
    }
}
