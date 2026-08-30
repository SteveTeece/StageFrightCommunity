using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Layout;

/// <summary>
/// Cascading component that owns the culture the app is presenting in (spec 027, US3 / T059 —
/// plan.md Decision 6; spec 029 adds the live in-session switch). It cascades the process
/// culture resolved at startup by <c>ILanguageProvider</c> (FR-023), and its <see cref="Switch"/>
/// method changes it live: a language choice now applies within the running session, with no
/// restart (spec 029 FR-004/FR-008/FR-010/FR-020).
/// </summary>
/// <remarks>
/// Named <c>CultureProvider</c> rather than <c>LanguageProvider</c> to avoid colliding with the
/// Core <see cref="StageFright.Core.Modules.Localization.LanguageProvider"/> startup service.
/// Parallel to <see cref="ThemeProvider"/>.
/// </remarks>
public partial class CultureProvider : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// The culture the whole app is currently presenting in — the one
    /// <c>MauiProgram</c> applied to <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> at
    /// startup, or whatever <see cref="Switch"/> has since changed it to.
    /// </summary>
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    protected override void OnInitialized()
    {
        CurrentCulture = CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// Switches the running session to <paramref name="culture"/> immediately: sets it on
    /// <see cref="CultureInfo.DefaultThreadCurrentCulture"/>, <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>,
    /// <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/>,
    /// updates <see cref="CurrentCulture"/>, then re-renders. Does not persist anything —
    /// persistence is always the caller's job (<c>ILanguagePreferenceStore.Set</c> /
    /// <c>SettingsService.SaveAsync</c>).
    /// </summary>
    public void Switch(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CurrentCulture = culture;
        StateHasChanged();
    }
}
