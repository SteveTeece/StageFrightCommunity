using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.Localization.Tests;

/// <summary>
/// US3 (T047) — proves the <c>qps-ploc</c> pseudo-locale added as a drop-in resource set
/// (Decision 9): the runtime catalog never lists it (SC-003); selecting it re-presents the app
/// (a key present in the pseudo set resolves to its bracketed pseudo value); and a key
/// deliberately omitted from the pseudo set falls back key-by-key to the Australian English
/// value — never a blank, never the raw key (FR-008 / SC-004). A key absent from every set
/// still logs the missing-key Warning (FR-009).
/// </summary>
public sealed class Us3PseudoLocaleGuardTests
{
    private const string PseudoCulture = "qps-ploc";

    // Present in SharedResource.resx and pseudo-localised in SharedResource.qps-ploc.resx.
    private const string TranslatedKey = "Shared_Action_Save";

    // Present in SharedResource.resx, DELIBERATELY omitted from SharedResource.qps-ploc.resx
    // (scripts/generate-pseudo-locale.py OMIT list) so per-key fallback is exercised.
    private const string OmittedKey = "Shared_Action_Cancel";

    [Fact]
    public void Should_NeverListPseudoLocale_When_CatalogBuiltAtRuntime()
    {
        var catalog = new SupportedLanguagesCatalog();

        Assert.DoesNotContain(catalog.All, l => l.CultureCode.StartsWith("qps", StringComparison.OrdinalIgnoreCase));
        Assert.Null(catalog.Find(PseudoCulture));
    }

    [Fact]
    public void Should_RePresentTheApp_When_PseudoLocaleIsActive()
    {
        var value = WithCulture(PseudoCulture, () => NeutralFactory().Create(typeof(SharedResource))[TranslatedKey]);

        Assert.False(value.ResourceNotFound);
        Assert.NotEqual(TranslatedKey, value.Value);           // not the raw key
        Assert.Contains("⟦", value.Value, StringComparison.Ordinal); // the pseudo-localised form
    }

    [Fact]
    public void Should_FallBackToAustralianEnglish_When_KeyOmittedFromThePseudoSet()
    {
        var neutral = NeutralFactory().Create(typeof(SharedResource))[OmittedKey].Value;
        var underPseudo = WithCulture(PseudoCulture, () => NeutralFactory().Create(typeof(SharedResource))[OmittedKey]);

        Assert.False(string.IsNullOrEmpty(underPseudo.Value));
        Assert.NotEqual(OmittedKey, underPseudo.Value);        // never the raw key
        Assert.Equal(neutral, underPseudo.Value);              // exactly the en-AU value
        Assert.DoesNotContain("⟦", underPseudo.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_LogWarningAndFallBack_When_KeyAbsentFromEverySetUnderThePseudoLocale()
    {
        var logger = new CapturingLogger<MissingKeyLoggingLocalizerFactory>();
        var factory = new MissingKeyLoggingLocalizerFactory(
            new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance),
            logger);

        var result = WithCulture(PseudoCulture, () => factory.Create(typeof(SharedResource))["Shared_Guard_KeyThatDoesNotExistAnywhere"]);

        Assert.True(result.ResourceNotFound);
        Assert.Equal("Shared_Guard_KeyThatDoesNotExistAnywhere", result.Value); // neutral fallback = key name, never blank
        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Missing localization key", StringComparison.Ordinal));
    }

    // --- helpers ----------------------------------------------------------------------

    private static IStringLocalizerFactory NeutralFactory() =>
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);

    private static T WithCulture<T>(string cultureName, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<(LogLevel Level, string Message)> Entries = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
