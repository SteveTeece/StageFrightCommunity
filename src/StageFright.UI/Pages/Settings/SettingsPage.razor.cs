using Microsoft.AspNetCore.Components;
using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Settings;

public partial class SettingsPage : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tab")]
    private string? TabQuery { get; set; }

    private string ActiveTabKey { get; set; } = string.Empty;

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

        if (!string.IsNullOrEmpty(TabQuery) && seen.Contains(TabQuery))
            ActiveTabKey = TabQuery;
        else
            ActiveTabKey = OrderedTabs.FirstOrDefault()?.TabKey ?? string.Empty;
    }

    private void ActivateTab(string tabKey)
    {
        ActiveTabKey = tabKey;
        Nav.NavigateTo($"/settings?tab={tabKey}", replace: true);
    }
}
