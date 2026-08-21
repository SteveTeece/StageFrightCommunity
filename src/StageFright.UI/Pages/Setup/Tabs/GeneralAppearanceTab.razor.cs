using Microsoft.AspNetCore.Components;
using StageFright.Core.Enums;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>General tab (US1/US6) — organisation name + theme dropdown, consistent with
/// every other yes/no-or-choice control elsewhere in the wizard (FR-022).</summary>
public partial class GeneralAppearanceTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private async Task HandleThemeChangedAsync(ChangeEventArgs e)
    {
        if (ThemeProvider is null)
            return;

        var selected = string.Equals(e.Value?.ToString(), nameof(Theme.Dark), StringComparison.Ordinal)
            ? Theme.Dark
            : Theme.Light;

        // ThemeProvider only exposes ToggleAsync (a straight Light<->Dark flip) — calling
        // it only when the selection actually differs from the current theme is enough to
        // land on whichever of the two the dropdown was set to.
        if (selected != ThemeProvider.CurrentTheme)
            await ThemeProvider.ToggleAsync();
    }
}
