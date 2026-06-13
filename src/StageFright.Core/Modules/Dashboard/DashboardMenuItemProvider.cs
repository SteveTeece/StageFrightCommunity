using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Dashboard;

/// <summary>
/// Contributes the Dashboard navigation item. DisplayOrder=0 places it first in the shell menu.
/// </summary>
public class DashboardMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Dashboard";
    public int DisplayOrder => 0;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = "Dashboard",
            Route = "/dashboard",
            DisplayOrder = 0
        }
    ];
}
