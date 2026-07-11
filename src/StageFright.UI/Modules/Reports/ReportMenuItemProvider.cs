using StageFright.Plugins.Contracts;
using StageFright.Reports.Registry;

namespace StageFright.UI.Modules.Reports;

/// <summary>
/// Contributes the Reports navigation group. Each registered report is a
/// sub-item, mirroring FinanceMenuItemProvider's expandable-group pattern.
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
        var subItems = new List<MenuItem>();
        var order = 0;
        foreach (var section in _registry.GetMenuSections())
        {
            foreach (var entry in section.Reports)
            {
                subItems.Add(new MenuItem
                {
                    Title = entry.ReportName,
                    Route = $"/reports/{entry.ReportId}",
                    DisplayOrder = order++
                });
            }
        }

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
