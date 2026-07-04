using Microsoft.AspNetCore.Components;
using StageFright.Plugins.Contracts;

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
    /// Maps a top-level route to the geometric dock glyph defined in app.css.
    /// Unknown routes (external plugins) get the generic plugin glyph.
    /// </summary>
    private static string GlyphClass(string route)
    {
        var root = route.TrimStart('/').Split('/', '?')[0].ToLowerInvariant();
        return root switch
        {
            "dashboard" => "glyph-home",
            "members" => "glyph-members",
            "finance" => "glyph-finance",
            "rehearsals" => "glyph-rehearsals",
            "events" => "glyph-events",
            "reports" => "glyph-reports",
            "settings" => "glyph-settings",
            _ => "glyph-plugin"
        };
    }

    /// <summary>
    /// Dock label under the glyph: the provider-supplied ShortLabel, or the first
    /// four characters of the title uppercased as a fallback for plugin items.
    /// </summary>
    private static string ShortLabelFor(MenuItem item) =>
        !string.IsNullOrWhiteSpace(item.ShortLabel)
            ? item.ShortLabel
            : item.Title.Length <= 4
                ? item.Title.ToUpperInvariant()
                : item.Title[..4].ToUpperInvariant();
}
