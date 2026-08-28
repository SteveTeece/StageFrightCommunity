using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Dashboard;
using StageFright.Plugins.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Dashboard;

public partial class Dashboard
{
    [Inject] private IDashboardService DashboardService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IStringLocalizer<DashboardResource> L { get; set; } = null!;

    private IReadOnlyList<IDashboardTileProvider> _coreTiles = [];
    private IReadOnlyList<IDashboardTileProvider> _extensionTiles = [];
    private Dictionary<string, Task<TileLoadResult>> _loadTasks = new();
    private bool _initialized;

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
