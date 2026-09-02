using System.Globalization;

namespace StageFright.Core.Contracts;

/// <summary>
/// Resolves the display <see cref="CultureInfo"/> to apply at process startup (spec 027, FR-023).
/// The whole app — Blazor render, QuestPDF reports, date/number formatting — runs on the one
/// culture this returns; it is set on <see cref="CultureInfo.DefaultThreadCurrentCulture"/> /
/// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> before the first Blazor render.
/// </summary>
public interface ILanguageProvider
{
    /// <summary>
    /// Resolves the culture to apply at startup, in order (FR-023):
    /// <list type="number">
    ///   <item>an explicit <c>Settings.LanguageCode</c> that names a shipped language;</item>
    ///   <item>otherwise the operating-system display language, matched in the catalog by exact
    ///     culture then by parent language;</item>
    ///   <item>otherwise the default culture (<c>en-AU</c>).</item>
    /// </list>
    /// Never throws — an unresolvable stored code or OS culture simply drops to the next step
    /// (FR-017).
    /// </summary>
    Task<CultureInfo> ResolveStartupCultureAsync(CancellationToken ct = default);

    /// <summary>The default (ultimate fallback) culture — <c>en-AU</c>.</summary>
    CultureInfo DefaultCulture { get; }
}
