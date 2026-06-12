namespace StageFright.Plugins.Contracts;

/// <summary>
/// Implemented by modules and plugins to contribute navigation items to the shell menu.
/// Rendering order: Dashboard (DisplayOrder=0) → module items by DisplayOrder → plugin items → Settings (last).
/// Route conflicts are logged and the later registrant is skipped.
/// </summary>
public interface IMenuItemProvider
{
    /// <summary>Module name used for grouping (e.g., "Members", "Finance").</summary>
    string ModuleName { get; }

    /// <summary>Sort position among all menu providers.</summary>
    int DisplayOrder { get; }

    /// <summary>Returns the menu items contributed by this provider.</summary>
    IReadOnlyList<MenuItem> GetMenuItems();
}
