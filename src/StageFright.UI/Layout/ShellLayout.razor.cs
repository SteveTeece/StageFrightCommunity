using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Layout;

public partial class ShellLayout : LayoutComponentBase
{
    private ThemeProvider? _themeProvider;

    private bool IsActive(string route) =>
        Nav.Uri.EndsWith(route, StringComparison.OrdinalIgnoreCase);

    private void Navigate(string route) => Nav.NavigateTo(route);

    private async Task ToggleThemeAsync()
    {
        if (_themeProvider is not null)
            await _themeProvider.ToggleAsync();
    }

    /// <summary>
    /// Maps a top-level route to the sidebar icon defined in app.css.
    /// Unknown routes (external plugins) get the generic plugin icon.
    /// </summary>
    private static string IconClass(string route)
    {
        var root = route.TrimStart('/').Split('/', '?')[0].ToLowerInvariant();
        return root switch
        {
            "dashboard" => "icon-dashboard",
            "members" => "icon-members",
            "finance" => "icon-finance",
            "rehearsals" => "icon-rehearsals",
            "events" => "icon-events",
            "reports" => "icon-reports",
            "settings" => "icon-settings",
            _ => "icon-plugin"
        };
    }
}
