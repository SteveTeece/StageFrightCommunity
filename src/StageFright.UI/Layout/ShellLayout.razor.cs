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
}
