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

    /// <summary>
    /// Switches the running session to <paramref name="culture"/> immediately: assigns it to the
    /// process-wide <see cref="CultureInfo.DefaultThreadCurrentCulture"/> /
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> globals, updates
    /// <see cref="CurrentCulture"/>, then re-renders. Does not persist anything — persistence is
    /// always the caller's job (<c>ILanguagePreferenceStore.Set</c> / <c>SettingsService.SaveAsync</c>).
    /// </summary>
    /// <remarks>
    /// Deliberately sets <b>only</b> the two <c>DefaultThreadCurrent*</c> globals, never
    /// <see cref="CultureInfo.CurrentCulture"/> / <see cref="CultureInfo.CurrentUICulture"/>. The
    /// latter pair is <see cref="System.Threading.AsyncLocal{T}"/>-backed per-execution-context
    /// state: a value set here (on an event-handler continuation's context) is unwound before the
    /// next render batch runs, and any value pinned on the renderer's own execution context
    /// <i>shadows</i> the globals — so every <c>IStringLocalizer["Key"]</c> and
    /// <see cref="StageFright.Core.Localization.MoneyFormatter"/> call on later renders would keep
    /// reading the pre-switch culture until a full process restart (spec 029, the T036 defect).
    /// With no per-context override anywhere — <c>MauiProgram.RunStartupSequence</c> is matched to
    /// set only these same two globals — every render reads
    /// <see cref="CultureInfo.CurrentUICulture"/> straight through to
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> and the switch takes effect at once.
    /// </remarks>
    public void Switch(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CurrentCulture = culture;
        StateHasChanged();
    }
}
