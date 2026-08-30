namespace StageFright.Core.Localization;

/// <summary>
/// Area-agnostic access to localized strings, wrapping <see cref="Microsoft.Extensions.Localization.IStringLocalizerFactory"/>
/// so a missing key for the active culture logs a warning and falls back to the Australian
/// English (neutral) value — never a blank or the raw key (FR-008/FR-009). Consumers that
/// prefer the framework type may instead inject <c>IStringLocalizer&lt;TResource&gt;</c>
/// directly; this facade exists for call sites (e.g. a loop over report columns) where naming
/// an area marker per lookup would be noisy.
/// </summary>
public interface ILocalizer
{
    /// <summary>Resolves <paramref name="key"/> from the <typeparamref name="TResource"/> area.</summary>
    string Get<TResource>(string key);

    /// <summary>
    /// Resolves <paramref name="key"/> from the <typeparamref name="TResource"/> area and
    /// substitutes its named placeholders (e.g. <c>{OrganisationName}</c>) with
    /// <paramref name="args"/>, in the order the placeholders first appear in the resolved text
    /// (FR-010).
    /// </summary>
    string Get<TResource>(string key, params object[] args);

    /// <summary>
    /// Resolves the plural form of <paramref name="key"/> — <c>key + "_One"</c> when
    /// <paramref name="count"/> is 1, otherwise <c>key + "_Other"</c> — binding
    /// <paramref name="count"/> as the leading <c>{Count}</c> placeholder ahead of
    /// <paramref name="args"/>.
    /// </summary>
    string Plural<TResource>(string key, int count, params object[] args);

    /// <summary>
    /// Renders a user-facing enum value via <c>EnumsResource["Enum_&lt;TypeName&gt;_&lt;Member&gt;"]</c>
    /// (FR-024) — the only sanctioned alternative to <c>enum.ToString()</c> at a display site.
    /// </summary>
    string Enum(System.Enum value);
}
