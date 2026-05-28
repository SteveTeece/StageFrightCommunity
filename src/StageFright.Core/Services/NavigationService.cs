namespace StageFright.Core.Services;

/// <summary>
/// Navigation service interface to manage module links and enforce NavigateTo-only navigation per NFR-001.
/// Provides centralized navigation control for the application.
/// </summary>
public interface INavigationService
{
    /// <summary>Navigate to a specific module or page</summary>
    /// <param name="routePath">The route path to navigate to (e.g., "/members", "/rehearsals")</param>
    void NavigateTo(string routePath);

    /// <summary>Navigate and replace history (back button won't return to current page)</summary>
    void NavigateTo(string routePath, bool forceLoad);

    /// <summary>Get the current route</summary>
    string? GetCurrentRoute();

    /// <summary>Register a navigation interceptor for permission checks</summary>
    void RegisterInterceptor(Func<string, bool> interceptor);

    /// <summary>Get all available module routes</summary>
    IEnumerable<(string Path, string DisplayName)> GetModuleRoutes();
}

/// <summary>
/// Default implementation of INavigationService.
/// Manages application-wide navigation with centralized route control.
/// </summary>
public class NavigationService : INavigationService
{
    private string? _currentRoute;
    private Func<string, bool>? _interceptor;

    private static readonly Dictionary<string, string> ModuleRoutes = new()
    {
        { "/dashboard", "Dashboard" },
        { "/members", "Members" },
        { "/rehearsals", "Rehearsals" },
        { "/events", "Events" },
        { "/finance", "Finance" },
        { "/reports", "Reports" },
        { "/settings", "Settings" }
    };

    public void NavigateTo(string routePath)
    {
        NavigateTo(routePath, false);
    }

    public void NavigateTo(string routePath, bool forceLoad)
    {
        // Check interceptor permission if registered
        if (_interceptor != null && !_interceptor(routePath))
        {
            System.Diagnostics.Debug.WriteLine($"Navigation denied by interceptor: {routePath}");
            return;
        }

        if (!ModuleRoutes.ContainsKey(routePath))
        {
            System.Diagnostics.Debug.WriteLine($"Invalid route: {routePath}");
            return;
        }

        _currentRoute = routePath;
    }

    public string? GetCurrentRoute()
    {
        return _currentRoute;
    }

    public void RegisterInterceptor(Func<string, bool> interceptor)
    {
        _interceptor = interceptor;
    }

    public IEnumerable<(string Path, string DisplayName)> GetModuleRoutes()
    {
        return ModuleRoutes.Select(kvp => (kvp.Key, kvp.Value));
    }
}
