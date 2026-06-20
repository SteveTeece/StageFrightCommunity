using Microsoft.AspNetCore.Components;
using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Settings;

public partial class SettingsPage : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tab")]
    private string? TabQuery { get; set; }

    private int DefaultTabIndex { get; set; }

    private IReadOnlyList<ISettingsTabProvider> OrderedTabs { get; set; } = Array.Empty<ISettingsTabProvider>();

    protected override void OnInitialized()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tabs = new List<ISettingsTabProvider>();

        foreach (var tab in TabProviders.OrderBy(t => t.DisplayOrder))
        {
            if (seen.Add(tab.TabKey))
                tabs.Add(tab);
        }

        OrderedTabs = tabs;

        if (!string.IsNullOrEmpty(TabQuery))
        {
            var idx = OrderedTabs.ToList().FindIndex(
                t => string.Equals(t.TabKey, TabQuery, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                DefaultTabIndex = idx;
        }
    }

    private void NavToTab(string key) =>
        Nav.NavigateTo($"/settings?tab={key}", replace: true);
}
