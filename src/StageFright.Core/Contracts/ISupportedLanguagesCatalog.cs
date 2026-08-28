using StageFright.Core.Modules.Localization;

namespace StageFright.Core.Contracts;

/// <summary>
/// The set of display languages the application ships, <b>discovered at runtime</b> from the
/// resource cultures actually present in the build — the neutral <c>en-AU</c> set plus every
/// <c>&lt;Marker&gt;.&lt;culture&gt;.resx</c> satellite culture in the loaded resource assemblies
/// (FR-011, resolved 2026-08-27). There is no hand-maintained list; adding a language is a
/// drop-in resource set. Pseudo-locales (culture name <c>qps-*</c>) are excluded so the test
/// pseudo-locale never appears in the picker or in FR-023 system-language matching.
/// </summary>
public interface ISupportedLanguagesCatalog
{
    /// <summary>All shipped languages, ordered for the picker: default first, then by endonym.</summary>
    IReadOnlyList<SupportedLanguage> All { get; }

    /// <summary>The <see cref="SupportedLanguage.IsDefault"/> entry — <c>en-AU</c>.</summary>
    SupportedLanguage Default { get; }

    /// <summary>
    /// The catalog entry for <paramref name="cultureCode"/>, or <c>null</c> when the code is
    /// null, blank, or names a language this build does not ship (e.g. a stale
    /// <c>Settings.LanguageCode</c> left by a downgraded install).
    /// </summary>
    SupportedLanguage? Find(string? cultureCode);
}
