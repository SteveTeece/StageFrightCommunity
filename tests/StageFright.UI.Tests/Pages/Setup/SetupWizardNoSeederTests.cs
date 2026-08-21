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
/// bUnit tests for SetupWizard when IDebugDataSeeder is not registered — the state MauiProgram
/// leaves DI in for Release builds. There is never a database seed in Release, so the "Load
/// sample data" checkbox and its progress overlay must not render, and Finish must complete
/// without attempting to resolve or invoke the seeder.
/// </summary>
public class SetupWizardNoSeederTests : BunitContext
{
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();

    public SetupWizardNoSeederTests()
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

    private static void AdvanceToReview(IRenderedComponent<SetupWizard> cut)
    {
        cut.Find("#orgName").Change("My Choir");
        cut.Find("#btn-next").Click(); // -> Sales Tax
        cut.Find("#btn-next").Click(); // -> Committee
        cut.Find("#btn-next").Click(); // -> Chart of Accounts
        cut.Find("#btn-next").Click(); // -> Opening Balances
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

        // No seeder is registered, so the Finish gate (FR-021) can only be satisfied by a
        // real queued balance. OpeningBalancesTab hosts its own nested <EditForm>, which
        // bUnit's AngleSharp-parsed DOM can't simulate a submit through (see
        // SetupWizardTests' ChartOfAccountsTab tests for the same limitation) — invoking
        // its OnSubmit parameter directly exercises the same wiring without that quirk.
        var tab = cut.FindComponent<OpeningBalancesTab>();
        await cut.InvokeAsync(() => tab.Instance.OnSubmit.InvokeAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = DateTime.Today,
            Entries = new List<OpeningBalanceEntry> { new() { AccountId = Guid.NewGuid(), Amount = 100m } }
        }));

        await cut.Find("form").SubmitAsync();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
        Assert.DoesNotContain("setup-seeding-overlay", cut.Markup);
    }
}
