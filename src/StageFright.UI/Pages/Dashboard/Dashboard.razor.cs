using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Dashboard;
using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Dashboard;

public partial class Dashboard
{
    [Inject] private IDashboardService DashboardService { get; set; } = null!;

    private IReadOnlyList<IDashboardTileProvider> _coreTiles = [];
    private IReadOnlyList<IDashboardTileProvider> _extensionTiles = [];
    private Dictionary<string, Task<TileLoadResult>> _loadTasks = new();
    private bool _initialized;

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
