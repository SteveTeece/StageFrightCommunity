using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Localization;
using StageFright.UI.Pages.Settings;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Localization;

/// <summary>
/// bUnit tests for the spec 027 US3 language pickers: the Settings General tab and the Setup
/// Wizard step list every shipped language by its endonym, mark the active one, and the Settings
/// picker shows an inline restart notice at the point of change (FR-012 / FR-021). A 2-entry
/// fake catalog stands in so the "changed" path is observable while en-AU is the only real set.
/// </summary>
public sealed class LanguagePickerRenderTests : LocalizedTestContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public LanguagePickerRenderTests()
    {
        Services.AddSingleton<ISupportedLanguagesCatalog>(new FakeCatalog("en-AU", "fr-FR"));
        Services.AddSingleton(_settingsService);
    }

    private static AppSettings MakeSettings(string? languageCode = null) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        LanguageCode = languageCode,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void GeneralSettingsTab_ListsEveryShippedLanguageByEndonym()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        var options = cut.Find("#languageCode").QuerySelectorAll("option");
        Assert.Equal(2, options.Length);
        Assert.Equal("en-AU", options[0].GetAttribute("value"));
        Assert.Contains("English", options[0].TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("fr-FR", options[1].GetAttribute("value"));
    }

    [Fact]
    public void GeneralSettingsTab_MarksThePersistedLanguageAsActive()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings(languageCode: "fr-FR"));

        var cut = Render<GeneralSettingsTab>();

        Assert.Equal("fr-FR", cut.Find("#languageCode").GetAttribute("value"));
    }

    [Fact]
    public void GeneralSettingsTab_ShowsNoRestartNotice_BeforeAnyChange()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        Assert.DoesNotContain("Restart the app", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneralSettingsTab_ShowsRestartNotice_AfterTheSelectionChanges()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();
        cut.Find("#languageCode").Change("fr-FR");

        Assert.Contains("Restart the app", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeneralSettingsTab_PersistsTheSelectedLanguage_OnSave()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        cut.Find("#languageCode").Change("fr-FR");
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s!.LanguageCode == "fr-FR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SetupLanguageStep_ListsEndonymsAndPreSelectsTheDefault()
    {
        var model = new SetupFormModel();

        var cut = Render<LanguageSelectionTab>(p => p.Add(x => x.Model, model));

        var options = cut.Find("#languageSelect").QuerySelectorAll("option");
        Assert.Equal(2, options.Length);
        Assert.Contains("English", options[0].TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("en-AU", model.LanguageCode);
    }

    [Fact]
    public void SetupLanguageStep_WritesTheChoiceOntoTheModel()
    {
        var model = new SetupFormModel();
        var cut = Render<LanguageSelectionTab>(p => p.Add(x => x.Model, model));

        cut.Find("#languageSelect").Change("fr-FR");

        Assert.Equal("fr-FR", model.LanguageCode);
    }

    private sealed class FakeCatalog : ISupportedLanguagesCatalog
    {
        public FakeCatalog(params string[] codes) =>
            All = codes.Select(c => new SupportedLanguage(c, string.Equals(c, "en-AU", StringComparison.OrdinalIgnoreCase))).ToList();

        public IReadOnlyList<SupportedLanguage> All { get; }

        public SupportedLanguage Default => All.First(l => l.IsDefault);

        public SupportedLanguage? Find(string? cultureCode) =>
            string.IsNullOrWhiteSpace(cultureCode)
                ? null
                : All.FirstOrDefault(l => string.Equals(l.CultureCode, cultureCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
