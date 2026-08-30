using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Dashboard;

/// <summary>
/// Contributes the Dashboard navigation item. DisplayOrder=0 places it first in the shell menu.
/// </summary>
public class DashboardMenuItemProvider : IMenuItemProvider
{
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public DashboardMenuItemProvider(IStringLocalizer<NavigationResource> localizer)
    {
        _localizer = localizer;
    }

    public string ModuleName => "Dashboard";
    public int DisplayOrder => 0;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = _localizer["Nav_Dashboard_Title"],
            Route = "/dashboard",
            ShortLabel = _localizer["Nav_Dashboard_ShortLabel"],
            DisplayOrder = 0
        }
    ];
}
