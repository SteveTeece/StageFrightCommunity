using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Contributes the Settings navigation item as the last entry in the main menu bar.
/// DisplayOrder=999 ensures Settings always appears after all other module providers.
/// </summary>
public class SettingsMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Settings";
    public int DisplayOrder => 999;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = "Settings",
            Route = "/settings",
            DisplayOrder = 0
        }
    ];
}
