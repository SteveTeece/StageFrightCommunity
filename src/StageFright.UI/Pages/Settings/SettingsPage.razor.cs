using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Settings;

public partial class SettingsPage : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tab")]
    private string? TabQuery { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IEnumerable<ISettingsTabProvider> TabProviders { get; set; } = null!;
    [Inject] private ILogger<SettingsPage> Logger { get; set; } = null!;

    private int DefaultTabIndex { get; set; }
    private IReadOnlyList<ISettingsTabProvider> PluginTabs { get; set; } = Array.Empty<ISettingsTabProvider>();

    // Lazy-render flags: each tab's content is only instantiated when the tab is first shown.
    // This prevents multiple tab components from accessing the shared DbContext concurrently.
    internal bool GeneralShown;
    internal bool CategoriesShown;
    internal bool EventTypesShown;
    internal bool BackupShown;

    protected override void OnInitialized()
    {
        Logger.LogInformation("SettingsPage.OnInitialized start. TabQuery={TabQuery}", TabQuery);
        try
        {
            Logger.LogDebug("SettingsPage: resolving plugin tab providers");
            PluginTabs = TabProviders.OrderBy(t => t.DisplayOrder).ToList();
            Logger.LogDebug("SettingsPage: resolved {Count} plugin tab provider(s)", PluginTabs.Count);

            DefaultTabIndex = TabQuery?.ToLowerInvariant() switch
            {
                "categories" => 1,
                "event-types" => 2,
                "backup" => 3,
                _ => 0
            };
            Logger.LogDebug("SettingsPage: DefaultTabIndex={DefaultTabIndex}", DefaultTabIndex);

            // Eagerly show only the default-active tab; all others render lazily on first click.
            switch (DefaultTabIndex)
            {
                case 1: CategoriesShown = true; break;
                case 2: EventTypesShown = true; break;
                case 3: BackupShown = true; break;
                default: GeneralShown = true; break;
            }

            Logger.LogInformation("SettingsPage.OnInitialized complete. DefaultTabIndex={DefaultTabIndex} GeneralShown={GeneralShown}", DefaultTabIndex, GeneralShown);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsPage.OnInitialized threw an unhandled exception");
            throw;
        }
    }

    // OnClick fires synchronously from the user gesture before the Bootstrap animation
    // begins, making it far more reliable than OnShown (which fires via a JS transition
    // callback that can be missed in the MAUI WebView). Blazor automatically calls
    // StateHasChanged after an EventCallback, so no manual call is needed.
    internal void OnGeneralClicked()    { GeneralShown    = true; NavToTab("general"); }
    internal void OnCategoriesClicked() { CategoriesShown = true; NavToTab("categories"); }
    internal void OnEventTypesClicked() { EventTypesShown = true; NavToTab("event-types"); }
    internal void OnBackupClicked()     { BackupShown     = true; NavToTab("backup"); }

    private void NavToTab(string key) =>
        Nav.NavigateTo($"/settings?tab={key}", replace: true);
}
