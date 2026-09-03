using System.Globalization;
using StageFright.Core.Contracts;

namespace StageFright.Core.Modules.Localization;

/// <summary>
/// Default <see cref="ILanguageProvider"/> — resolves the startup culture with the FR-006 ladder
/// (spec 029, extending spec 027's FR-023): an explicit <c>Settings.LanguageCode</c> that names a
/// shipped language, else a recorded <see cref="ILanguagePreferenceStore"/> preference naming a
/// shipped language, else the OS display language when the catalog has an exact or
/// parent-language match, else <c>en-AU</c>. Every step is wrapped so an unreadable setting, an
/// unknown stored code, or an unresolvable OS culture drops to the next step rather than throwing
/// (FR-017).
/// </summary>
public sealed class LanguageProvider : ILanguageProvider
{
    private readonly ISettingsService _settingsService;
    private readonly ISupportedLanguagesCatalog _catalog;
    private readonly ISystemCultureProvider _systemCultureProvider;
    private readonly ILanguagePreferenceStore _languagePreferenceStore;

    public LanguageProvider(
        ISettingsService settingsService,
        ISupportedLanguagesCatalog catalog,
        ISystemCultureProvider systemCultureProvider,
        ILanguagePreferenceStore languagePreferenceStore)
    {
        _settingsService = settingsService;
        _catalog = catalog;
        _systemCultureProvider = systemCultureProvider;
        _languagePreferenceStore = languagePreferenceStore;
    }

    public CultureInfo DefaultCulture =>
        SafeCulture(_catalog.Default.CultureCode) ?? CultureInfo.GetCultureInfo(SupportedLanguagesCatalog.DefaultCultureCode);

    public async Task<CultureInfo> ResolveStartupCultureAsync(CancellationToken ct = default)
    {
        // (1) Explicit choice always wins (FR-014 / FR-023).
        try
        {
            var settings = await _settingsService.GetAsync(ct).ConfigureAwait(false);
            var explicitChoice = _catalog.Find(settings?.LanguageCode);
            if (explicitChoice is not null && SafeCulture(explicitChoice.CultureCode) is { } explicitCulture)
                return explicitCulture;
        }
        catch
        {
            // Settings unreadable (pre-first-run, DB error) — fall through to the OS language.
        }

        // (2) Recorded no-database preference, when it names a shipped language (spec 029,
        // FR-006 step 2) — read before the database exists, e.g. a first launch that recorded a
        // language on /language-select but hasn't completed setup yet.
        try
        {
            var recordedChoice = _catalog.Find(_languagePreferenceStore.Get());
            if (recordedChoice is not null && SafeCulture(recordedChoice.CultureCode) is { } recordedCulture)
                return recordedCulture;
        }
        catch
        {
            // Preference store unreadable — fall through to the OS language.
        }

        // (3) OS display language, when a matching resource set ships (FR-023 / SC-010).
        try
        {
            var osMatch = MatchOperatingSystemCulture(_systemCultureProvider.GetUiCulture());
            if (osMatch is not null && SafeCulture(osMatch.CultureCode) is { } osCulture)
                return osCulture;
        }
        catch
        {
            // OS culture unavailable — fall through to the default.
        }

        // (4) Ultimate fallback — en-AU.
        return DefaultCulture;
    }

    private SupportedLanguage? MatchOperatingSystemCulture(CultureInfo? osCulture)
    {
        if (osCulture is null
            || string.IsNullOrWhiteSpace(osCulture.Name)
            || osCulture.Equals(CultureInfo.InvariantCulture))
            return null;

        // Exact culture first (e.g. fr-CA), then the parent language (fr) — matched only among
        // the runtime-discovered shipped sets (FR-023, spec "System-language default" edge case).
        var exact = _catalog.Find(osCulture.Name);
        if (exact is not null)
            return exact;

        var parentLanguage = osCulture.TwoLetterISOLanguageName;
        if (string.IsNullOrWhiteSpace(parentLanguage) || string.Equals(parentLanguage, "iv", StringComparison.OrdinalIgnoreCase))
            return null;

        return _catalog.Find(parentLanguage)
            ?? _catalog.All.FirstOrDefault(l => HasParentLanguage(l.CultureCode, parentLanguage));
    }

    private static bool HasParentLanguage(string cultureCode, string parentLanguage)
    {
        var culture = SafeCulture(cultureCode);
        return culture is not null
            && string.Equals(culture.TwoLetterISOLanguageName, parentLanguage, StringComparison.OrdinalIgnoreCase);
    }

    private static CultureInfo? SafeCulture(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(cultureCode.Trim());
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
