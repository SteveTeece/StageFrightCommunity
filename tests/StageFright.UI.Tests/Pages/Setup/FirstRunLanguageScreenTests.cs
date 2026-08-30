using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for <see cref="FirstRunLanguageScreen"/> (spec 029) — the <c>/language-select</c>
/// screen shown before the setup wizard. Covers the language-selection path (US1) plus the
/// Debug-only "Load sample data" path (US3), which is absent whenever no
/// <see cref="IDebugDataSeeder"/> is registered (e.g. Release builds).
/// </summary>
public class FirstRunLanguageScreenTests : LocalizedTestContext
{
    private readonly ILanguagePreferenceStore _preferenceStore = Substitute.For<ILanguagePreferenceStore>();
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();

    public FirstRunLanguageScreenTests()
    {
        Services.AddSingleton(_preferenceStore);
        Services.AddSingleton(_setupService);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private IRenderedComponent<CultureProvider> RenderScreen() =>
        Render<CultureProvider>(p => p.AddChildContent<FirstRunLanguageScreen>());

    [Fact]
    public void ListsEveryCatalogLanguage_ByEndonym()
    {
        var cut = RenderScreen();
        var catalog = Services.GetRequiredService<ISupportedLanguagesCatalog>();

        var options = cut.Find("#languageSelect").QuerySelectorAll("option");

        Assert.Equal(catalog.All.Count, options.Length);
        foreach (var language in catalog.All)
        {
            Assert.Contains(options, o => o.GetAttribute("value") == language.CultureCode && o.TextContent == language.Endonym);
        }
    }

    [Fact]
    public async Task PreSelects_TheLanguageProviderResolvedDefault()
    {
        var languageProvider = Services.GetRequiredService<ILanguageProvider>();
        var catalog = Services.GetRequiredService<ISupportedLanguagesCatalog>();
        var resolvedCulture = await languageProvider.ResolveStartupCultureAsync(Xunit.TestContext.Current.CancellationToken);
        var expected = catalog.Find(resolvedCulture.Name)?.CultureCode ?? catalog.Default.CultureCode;

        var cut = RenderScreen();

        Assert.Equal(expected, cut.Find("#languageSelect").GetAttribute("value"));
    }

    [Fact]
    public void DoesNotRender_SampleDataControl_WhenNoDebugSeederRegistered()
    {
        var cut = RenderScreen();

        Assert.Empty(cut.FindAll("#seedData"));
    }

    [Fact]
    public void Confirm_RecordsThePreference()
    {
        using var _ = new CultureRestorer();
        var cut = RenderScreen();
        cut.Find("#languageSelect").Change("fr-FR");

        cut.Find("#btn-confirm-language").Click();

        _preferenceStore.Received(1).Set("fr-FR");
    }

    [Fact]
    public void Confirm_SwitchesTheRunningSessionCulture()
    {
        using var _ = new CultureRestorer();
        var cut = RenderScreen();
        cut.Find("#languageSelect").Change("fr-FR");

        cut.Find("#btn-confirm-language").Click();

        Assert.Equal("fr-FR", cut.Instance.CurrentCulture.Name);
    }

    [Fact]
    public void Confirm_Navigates_ToSetup_WhenSampleDataNotOffered()
    {
        using var _ = new CultureRestorer();
        var cut = RenderScreen();
        cut.Find("#languageSelect").Change("fr-FR");

        cut.Find("#btn-confirm-language").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/setup", nav.Uri);
    }

    [Fact]
    public void SampleDataControl_Renders_WhenDebugSeederRegistered()
    {
        Services.AddSingleton(Substitute.For<IDebugDataSeeder>());

        var cut = RenderScreen();

        cut.Find("#seedData");
    }

    [Fact]
    public async Task Confirm_WithSampleDataChecked_SeedsThenNavigatesToDashboard()
    {
        using var _ = new CultureRestorer();
        var debugSeeder = Substitute.For<IDebugDataSeeder>();
        debugSeeder.SeedAsync(Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        Services.AddSingleton(debugSeeder);

        var cut = RenderScreen();
        cut.Find("[role=switch]").Click();

        // Confirming here goes through a real Task.Run hop (seeding runs off the UI thread, same
        // as the deleted SetupWizard.HandleValidSubmitAsync did) — a synchronous Click() only
        // blocks for work that completes without a genuine thread switch, so this needs the async
        // ClickAsync() bUnit uses everywhere else for a background-work-bearing submit/click.
        await cut.Find("#btn-confirm-language").ClickAsync(new MouseEventArgs());

        Received.InOrder(() =>
        {
            _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>());
            debugSeeder.SeedAsync(Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());
        });
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/dashboard", nav.Uri);
    }

    [Fact]
    public async Task Confirm_WithSampleDataChecked_ShowsError_WhenSeedingFails()
    {
        using var _ = new CultureRestorer();
        var debugSeeder = Substitute.For<IDebugDataSeeder>();
        debugSeeder.SeedAsync(Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));
        Services.AddSingleton(debugSeeder);

        var cut = RenderScreen();
        cut.Find("[role=switch]").Click();

        // See the comment on Confirm_WithSampleDataChecked_SeedsThenNavigatesToDashboard — the
        // failing SeedAsync still runs through the same real Task.Run hop, so this needs
        // ClickAsync() too.
        await cut.Find("#btn-confirm-language").ClickAsync(new MouseEventArgs());

        var expectedError = Services.GetRequiredService<IStringLocalizer<SetupResource>>()["Setup_FirstRun_SeedingError"];
        Assert.Contains(expectedError, cut.Markup);
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotContain("/dashboard", nav.Uri);
        await _setupService.Received(1).InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>());
    }
}
