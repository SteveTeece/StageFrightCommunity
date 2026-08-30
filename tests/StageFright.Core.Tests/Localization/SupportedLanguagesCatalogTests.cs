using StageFright.Core.Modules.Localization;

namespace StageFright.Core.Tests.Localization;

/// <summary>
/// Tests for <see cref="SupportedLanguagesCatalog"/>'s runtime discovery (FR-011): the shipped
/// language list is built from the resource cultures actually present in the build, always
/// contains the neutral <c>en-AU</c> baseline, derives endonyms from culture metadata, and
/// never surfaces a <c>qps-*</c> pseudo-locale — even though the <c>qps-ploc</c> satellite
/// assembly is copied into this test project's output (SC-003).
/// </summary>
public sealed class SupportedLanguagesCatalogTests
{
    private readonly SupportedLanguagesCatalog _catalog = new();

    [Fact]
    public void Should_AlwaysContainEnAuAsTheDefault()
    {
        var enAu = Assert.Single(_catalog.All, l => l.CultureCode == "en-AU");

        Assert.True(enAu.IsDefault);
        Assert.Equal("en-AU", _catalog.Default.CultureCode);
        Assert.Same(enAu, _catalog.Default);
    }

    [Fact]
    public void Should_ExcludePseudoLocale_When_QpsPlocSatelliteIsPresentInTheBuild()
    {
        Assert.DoesNotContain(_catalog.All, l => l.CultureCode.StartsWith("qps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_OrderDefaultFirst()
    {
        Assert.True(_catalog.All[0].IsDefault);
    }

    [Fact]
    public void Should_DeriveEndonymFromCultureMetadata()
    {
        var enAu = _catalog.Find("en-AU");

        Assert.NotNull(enAu);
        Assert.Contains("English", enAu!.Endonym, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("zz-ZZ")]
    [InlineData("not-a-culture")]
    public void Should_ReturnNull_When_FindCalledWithNullBlankOrUnknownCode(string? code)
    {
        Assert.Null(_catalog.Find(code));
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("en-au")]
    [InlineData("EN-AU")]
    [InlineData(" en-AU ")]
    public void Should_ResolveCaseInsensitivelyAndTrimmed_When_FindCalledWithAKnownCode(string code)
    {
        var found = _catalog.Find(code);

        Assert.NotNull(found);
        Assert.Equal("en-AU", found!.CultureCode);
    }

    [Fact]
    public void Should_EquateEntries_ByCultureCodeOnly()
    {
        Assert.Equal(new SupportedLanguage("en-AU", isDefault: true), new SupportedLanguage("en-AU", isDefault: false));
    }

    [Fact]
    public void Should_FallBackToTheDefaultAssemblies_When_ConstructedWithAnEmptyAssemblyList()
    {
        // A DI container resolves an unregistered IEnumerable<string> as an *empty* sequence,
        // not null — so an empty probe list must behave exactly like the parameterless
        // constructor (probe Core / UI / Reports) rather than disabling discovery and leaving
        // only en-AU (issue #360).
        var viaEmptyList = new SupportedLanguagesCatalog([]);

        Assert.Equal(
            new SupportedLanguagesCatalog().All.Select(l => l.CultureCode),
            viaEmptyList.All.Select(l => l.CultureCode));
        Assert.True(
            viaEmptyList.All.Count > 1,
            "the satellite resource sets shipped in the build (e.g. en-US) should be discovered");
    }
}
