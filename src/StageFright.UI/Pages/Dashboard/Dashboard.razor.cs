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

    protected override async Task OnInitializedAsync()
    {
        var allProviders = await DashboardService.GetTilesAsync();

        _coreTiles = allProviders.Where(p => p.DisplayOrder < 100).ToList();
        _extensionTiles = allProviders.Where(p => p.DisplayOrder >= 100).ToList();

        // Start all tile loads in parallel — callers await via Task.WhenAll or individually
        _loadTasks = allProviders.ToDictionary(
            p => p.TileId,
            p => DashboardService.LoadTileAsync(p));

        _initialized = true;
    }
}
