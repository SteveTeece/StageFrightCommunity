using System.Globalization;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Localization;

namespace StageFright.Core.Tests.Localization;

/// <summary>
/// Unit tests for <see cref="LanguageProvider"/>'s FR-023 startup ladder: an explicit
/// <c>Settings.LanguageCode</c> that names a shipped language wins; otherwise the OS display
/// language when the catalog matches it by exact culture then by parent language; otherwise
/// <c>en-AU</c>. Every step is wrapped so a failure drops to the next (FR-017 / SC-010).
/// </summary>
public sealed class LanguageProviderTests
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ISystemCultureProvider _systemCulture = Substitute.For<ISystemCultureProvider>();

    [Fact]
    public async Task Should_ReturnExplicitChoice_When_LanguageCodeNamesShippedLanguage()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith("fr-FR"));
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("en-US"));
        var provider = Build("en-AU", "fr-FR");

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("fr-FR", culture.Name);
    }

    [Fact]
    public async Task Should_PreferExplicitChoice_When_ItAndTheOsLanguageBothResolve()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith("en-AU"));
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("fr-FR"));
        var provider = Build("en-AU", "fr-FR");

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name);
    }

    [Fact]
    public async Task Should_UseOsLanguage_When_NoExplicitChoiceAndTheOsCultureShips()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith(null));
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("fr-FR"));
        var provider = Build("en-AU", "fr-FR");

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("fr-FR", culture.Name);
    }

    [Fact]
    public async Task Should_MatchParentLanguage_When_OsCultureIsARegionalVariantOfAShippedLanguage()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith(null));
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("en-US"));
        var provider = Build("en-AU"); // only en-AU ships

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name);
    }

    [Fact]
    public async Task Should_FallBackToEnAu_When_TheOsCultureHasNoShippedMatch()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith(null));
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("de-DE"));
        var provider = Build("en-AU");

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name);
    }

    [Fact]
    public async Task Should_FallBackToEnAu_When_TheOsCultureCannotBeResolved()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith(null));
        _systemCulture.GetUiCulture().Returns(CultureInfo.InvariantCulture);
        var provider = Build("en-AU");

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name);
    }

    [Fact]
    public async Task Should_IgnoreStoredCode_When_ItNamesALanguageThisBuildNoLongerShips()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith("mi-NZ"));
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("fr-FR"));
        var provider = Build("en-AU", "fr-FR"); // mi-NZ not shipped -> treated as no explicit choice

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("fr-FR", culture.Name);
    }

    [Fact]
    public async Task Should_NotThrowAndFallBackToEnAu_When_SettingsServiceThrows()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Settings?>>(_ => throw new InvalidOperationException("db unavailable"));
        _systemCulture.GetUiCulture().Returns(CultureInfo.InvariantCulture);
        var provider = Build("en-AU");

        var culture = await provider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name);
    }

    [Fact]
    public void Should_ExposeEnAu_When_DefaultCultureRead()
    {
        var provider = Build("en-AU", "fr-FR");

        Assert.Equal("en-AU", provider.DefaultCulture.Name);
    }

    // --- helpers -------------------------------------------------------------------

    private LanguageProvider Build(params string[] shippedCultureCodes) =>
        new(_settingsService, new FakeCatalog(shippedCultureCodes), _systemCulture);

    private static Task<Settings?> SettingsWith(string? languageCode) =>
        Task.FromResult<Settings?>(new Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Choir",
            LanguageCode = languageCode,
            SchemaVersion = "1.1.0",
        });

    private sealed class FakeCatalog : ISupportedLanguagesCatalog
    {
        public FakeCatalog(IEnumerable<string> codes) =>
            All = codes.Select(c => new SupportedLanguage(c, string.Equals(c, "en-AU", StringComparison.OrdinalIgnoreCase)))
                       .ToList();

        public IReadOnlyList<SupportedLanguage> All { get; }

        public SupportedLanguage Default => All.First(l => l.IsDefault);

        public SupportedLanguage? Find(string? cultureCode) =>
            string.IsNullOrWhiteSpace(cultureCode)
                ? null
                : All.FirstOrDefault(l => string.Equals(l.CultureCode, cultureCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
