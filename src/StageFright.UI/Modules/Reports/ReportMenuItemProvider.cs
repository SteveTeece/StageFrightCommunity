using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;
using StageFright.Reports.Registry;

namespace StageFright.UI.Modules.Reports;

/// <summary>
/// Contributes the Reports navigation group. Each registered report is a
/// sub-item, mirroring FinanceMenuItemProvider's expandable-group pattern.
/// DisplayOrder=5 places Reports after Finance and before Settings.
/// Individual report names come from each provider's own <c>ReportName</c>
/// (localized via <c>ReportsResource</c> — spec 027 T039); this provider only
/// owns the group's own <c>Title</c>/<c>ShortLabel</c>.
/// </summary>
public class ReportMenuItemProvider : IMenuItemProvider
{
    private readonly IReportProviderRegistry _registry;
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public ReportMenuItemProvider(IReportProviderRegistry registry, IStringLocalizer<NavigationResource> localizer)
    {
        _registry = registry;
        _localizer = localizer;
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
                Title = _localizer["Nav_Reports_Title"],
                Route = "/reports",
                ShortLabel = _localizer["Nav_Reports_ShortLabel"],
                DisplayOrder = DisplayOrder,
                SubItems = subItems
            }
        ];
    }
}
