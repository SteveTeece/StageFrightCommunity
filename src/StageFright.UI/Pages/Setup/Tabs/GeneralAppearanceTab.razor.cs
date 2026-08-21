using Microsoft.AspNetCore.Components;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>General tab (US1) — organisation name + theme, relocated unchanged from the
/// old single-page wizard's Step 1. US6 later swaps the theme control for a dropdown.</summary>
public partial class GeneralAppearanceTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private async Task HandleThemeToggleAsync()
    {
        if (ThemeProvider is not null)
            await ThemeProvider.ToggleAsync();
    }
}
