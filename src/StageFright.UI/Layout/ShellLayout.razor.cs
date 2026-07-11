using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using StageFright.Plugins.Contracts;

namespace StageFright.UI.Layout;

public partial class ShellLayout : LayoutComponentBase, IDisposable
{
    private ThemeProvider? _themeProvider;

    /// <summary>
    /// Explicit expand/collapse overrides keyed by the group's parent route.
    /// Absent = automatic (expanded only while a child route is active).
    /// </summary>
    private readonly Dictionary<string, bool> _groupOverrides = new(StringComparer.OrdinalIgnoreCase);

    protected override void OnInitialized()
    {
        Nav.LocationChanged += OnLocationChanged;
    }

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // Navigating re-enables auto-expansion so the group containing the
        // active route always opens, even after an explicit collapse.
        _groupOverrides.Clear();
        StateHasChanged();
    }

    private string CurrentPath
    {
        get
        {
            var relative = Nav.ToBaseRelativePath(Nav.Uri).Split('?', '#')[0];
            return "/" + relative.TrimStart('/');
        }
    }

    private bool IsActive(string route) =>
        string.Equals(CurrentPath, route, StringComparison.OrdinalIgnoreCase);

    /// <summary>Starts-with variant for group headers: active when the route or any descendant is current.</summary>
    private bool IsRouteOrDescendantActive(string route) =>
        IsActive(route) || CurrentPath.StartsWith(route + "/", StringComparison.OrdinalIgnoreCase);

    private bool IsParentActive(MenuItem item) =>
        IsRouteOrDescendantActive(item.Route) ||
        item.SubItems.Any(s => IsRouteOrDescendantActive(s.Route));

    private bool IsGroupExpanded(MenuItem item) =>
        _groupOverrides.TryGetValue(item.Route, out var expanded)
            ? expanded
            : IsParentActive(item);

    private void ToggleGroup(string route)
    {
        var item = FindGroup(route);
        var currentlyExpanded = item is not null && IsGroupExpanded(item);
        _groupOverrides[route] = !currentlyExpanded;
    }

    private MenuItem? FindGroup(string route) =>
        MenuProviders
            .SelectMany(p => p.GetMenuItems())
            .FirstOrDefault(i => string.Equals(i.Route, route, StringComparison.OrdinalIgnoreCase));

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
