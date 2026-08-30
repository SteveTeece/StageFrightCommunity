using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for <see cref="FirstRunLanguageScreen"/> (spec 029, US1) — the <c>/language-select</c>
/// screen shown before the setup wizard. Covers the language-selection path only; the Debug-only
/// "Load sample data" path is covered separately (US3 extends this same test file).
/// </summary>
public class FirstRunLanguageScreenTests : LocalizedTestContext
{
    private readonly ILanguagePreferenceStore _preferenceStore = Substitute.For<ILanguagePreferenceStore>();

    public FirstRunLanguageScreenTests()
    {
        Services.AddSingleton(_preferenceStore);
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
        var cut = RenderScreen();
        cut.Find("#languageSelect").Change("fr-FR");

        cut.Find("#btn-confirm-language").Click();

        _preferenceStore.Received(1).Set("fr-FR");
    }

    [Fact]
    public void Confirm_SwitchesTheRunningSessionCulture()
    {
        var cut = RenderScreen();
        cut.Find("#languageSelect").Change("fr-FR");

        cut.Find("#btn-confirm-language").Click();

        Assert.Equal("fr-FR", cut.Instance.CurrentCulture.Name);
    }

    [Fact]
    public void Confirm_Navigates_ToSetup_WhenSampleDataNotOffered()
    {
        var cut = RenderScreen();
        cut.Find("#languageSelect").Change("fr-FR");

        cut.Find("#btn-confirm-language").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/setup", nav.Uri);
    }
}
