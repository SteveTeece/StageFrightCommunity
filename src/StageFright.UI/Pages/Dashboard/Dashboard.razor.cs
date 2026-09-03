using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Dashboard;
using StageFright.Plugins.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Dashboard;

public partial class Dashboard
{
    [Inject] private IDashboardService DashboardService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IStringLocalizer<DashboardResource> L { get; set; } = null!;
    [Inject] private IStartupDiagnosticService StartupDiagnostics { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private IReadOnlyList<IDashboardTileProvider> _coreTiles = [];
    private IReadOnlyList<IDashboardTileProvider> _extensionTiles = [];
    private Dictionary<string, Task<TileLoadResult>> _loadTasks = new();
    private bool _initialized;

    /// <summary>
    /// Session-scoped dismissal of the non-fatal startup warning banner (spec 028, US8 / FR-025).
    /// The underlying diagnostic state is not cleared — only hidden until the next app start.
    /// </summary>
    private bool _startupWarningDismissed;

    private bool ShowStartupWarning => StartupDiagnostics.HasStartupWarning && !_startupWarningDismissed;

    // The banner text is always the localised string; StartupDiagnostics.StartupWarning holds the
    // non-localised diagnostic detail (also written to the log) and is not shown verbatim.
    private string StartupWarningText => Loc.Get<SharedResource>("Shared_StartupWarning_AuditPurgeFailed");

    private string StartupWarningDismissLabel => Loc.Get<SharedResource>("Shared_StartupWarning_DismissLabel");

    private void DismissStartupWarning() => _startupWarningDismissed = true;

    private void NavigateTo(string? route)
    {
        if (route != null)
            NavigationManager.NavigateTo(route);
    }

    private static string GetTileSizeClass(DashboardTileSize size) => size switch
    {
        DashboardTileSize.OneByTwo => "tile-size-1x2",
        DashboardTileSize.TwoByOne => "tile-size-2x1",
        DashboardTileSize.TwoByTwo => "tile-size-2x2",
        _ => "tile-size-1x1"
    };

    // OnParametersSetAsync fires on every navigation to /dashboard (component recreated OR reused),
    // ensuring tiles always reload fresh data rather than showing stale state from a prior visit.
    protected override async Task OnParametersSetAsync()
    {
        var allProviders = await DashboardService.GetTilesAsync();

        _coreTiles = allProviders.Where(p => p.DisplayOrder < 100).ToList();
        _extensionTiles = allProviders.Where(p => p.DisplayOrder >= 100).ToList();

        _loadTasks = allProviders.ToDictionary(
            p => p.TileId,
            p => DashboardService.LoadTileAsync(p));

        _initialized = true;
    }
}
