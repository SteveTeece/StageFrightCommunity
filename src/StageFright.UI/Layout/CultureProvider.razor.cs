using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Layout;

/// <summary>
/// Cascading component that owns the culture the app is presenting in (spec 027, US3 / T059 —
/// plan.md Decision 6). It is the <em>seam</em> for a future in-session live language switch:
/// today it simply cascades the process culture resolved at startup by <c>ILanguageProvider</c>
/// (FR-023) and changes nothing — a language change still applies on the next launch (FR-021).
/// A later story can add a <c>SwitchAsync</c> here that updates <see cref="CurrentCulture"/> and
/// re-renders, without touching any of the extracted call sites.
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
    /// startup. Read-only in v1.
    /// </summary>
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    protected override void OnInitialized()
    {
        CurrentCulture = CultureInfo.CurrentUICulture;
    }
}
