using System.Globalization;

namespace StageFright.Core.Modules.Localization;

/// <summary>
/// One display language the application ships as a user-selectable option (spec 027, FR-011 /
/// FR-012). Built at runtime from a shipped resource culture — never authored or persisted.
/// </summary>
public sealed record SupportedLanguage
{
    /// <summary>Constructs an entry, deriving <see cref="Endonym"/> from the culture's own metadata.</summary>
    /// <param name="cultureCode">BCP-47 id of a shipped resource set, e.g. <c>en-AU</c>.</param>
    /// <param name="isDefault"><c>true</c> for the neutral / baseline set (<c>en-AU</c>).</param>
    public SupportedLanguage(string cultureCode, bool isDefault)
    {
        CultureCode = cultureCode;
        IsDefault = isDefault;
        Endonym = BuildEndonym(cultureCode);
    }

    /// <summary>BCP-47 culture id — matches a shipped neutral or satellite <c>.resx</c> set.</summary>
    public string CultureCode { get; }

    /// <summary>
    /// The language's name in its own language, shown in the picker (FR-012), e.g.
    /// "English (Australia)". Derived from <see cref="CultureInfo.NativeName"/>, title-cased.
    /// </summary>
    public string Endonym { get; }

    /// <summary>
    /// <c>true</c> for the baseline (<c>en-AU</c>) set — used to pre-select the picker and to
    /// resolve a null/unknown <c>Settings.LanguageCode</c>.
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>Equality is by <see cref="CultureCode"/> only.</summary>
    public bool Equals(SupportedLanguage? other) =>
        other is not null && string.Equals(CultureCode, other.CultureCode, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(CultureCode);

    private static string BuildEndonym(string cultureCode)
    {
        try
        {
            var native = CultureInfo.GetCultureInfo(cultureCode).NativeName;
            if (string.IsNullOrWhiteSpace(native))
                return cultureCode;

            // NativeName is lower-cased for some cultures (e.g. "français (France)"); present
            // it title-cased in that culture so the picker reads naturally.
            return CultureInfo.GetCultureInfo(cultureCode).TextInfo.ToTitleCase(native);
        }
        catch (CultureNotFoundException)
        {
            return cultureCode;
        }
    }
}
