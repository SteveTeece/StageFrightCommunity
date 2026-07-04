using StageFright.Plugins.Contracts;
using StageFright.Reports.Registry;

namespace StageFright.UI.Modules.Reports;

/// <summary>
/// Contributes the Reports root menu item, with sub-items built dynamically
/// from all registered IReportProvider instances via IReportProviderRegistry.
/// DisplayOrder=5 places Reports after Finance and before Settings.
/// </summary>
public class ReportMenuItemProvider : IMenuItemProvider
{
    private readonly IReportProviderRegistry _registry;

    public ReportMenuItemProvider(IReportProviderRegistry registry)
    {
        _registry = registry;
    }

    public string ModuleName => "Reports";
    public int DisplayOrder => 5;

    public IReadOnlyList<MenuItem> GetMenuItems()
    {
        var subItems = _registry.GetMenuSections()
            .SelectMany(section => section.Reports.Select(r => new MenuItem
            {
                Title = r.ReportName,
                Route = $"/reports/{r.ReportId}",
                DisplayOrder = 0
            }))
            .ToList();

        return
        [
            new MenuItem
            {
                Title = "Reports",
                Route = "/reports",
                ShortLabel = "RPT",
                DisplayOrder = DisplayOrder,
                SubItems = subItems
            }
        ];
    }
}
