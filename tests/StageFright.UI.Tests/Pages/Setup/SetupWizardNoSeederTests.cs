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
    private const string ValidAbn = "51824753556";

    private readonly ISetupService _setupService = Substitute.For<ISetupService>();

    public SetupWizardNoSeederTests()
    {
        Services.AddSingleton(_setupService);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private static void AdvanceToReview(IRenderedComponent<SetupWizard> cut)
    {
        cut.Find("#orgName").Change("My Choir");
        cut.Find("#abn").Change(ValidAbn);
        cut.Find("#btn-next").Click(); // -> step 2
        cut.Find("#btn-next").Click(); // -> step 3
        cut.Find("#btn-next").Click(); // -> step 4 (committee config)
        cut.Find("#btn-next").Click(); // -> step 5 (Review & Finish)
    }

    [Fact]
    public void SeedDataCheckbox_IsAbsent_WhenSeederNotRegistered()
    {
        var cut = Render<SetupWizard>();
        AdvanceToReview(cut);

        Assert.Contains("Step 5 of 5", cut.Markup);
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
