using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Reports;

/// <summary>
/// Contributes the Reports root menu item. Individual reports are reached
/// from the Reports page itself rather than the navigation menu.
/// DisplayOrder=5 places Reports after Finance and before Settings.
/// </summary>
public class ReportMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Reports";
    public int DisplayOrder => 5;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = "Reports",
            Route = "/reports",
            ShortLabel = "RPT",
            DisplayOrder = DisplayOrder
        }
    ];
}
