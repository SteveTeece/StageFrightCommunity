using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Dashboard;

/// <summary>
/// Aggregates all registered IDashboardTileProvider instances, sorts them by DisplayOrder,
/// and loads tile data with per-tile exception isolation — a failing tile never blocks others.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IEnumerable<IDashboardTileProvider> _providers;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IEnumerable<IDashboardTileProvider> providers,
        ILogger<DashboardService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public Task<IReadOnlyList<IDashboardTileProvider>> GetTilesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<IDashboardTileProvider> sorted = [.. _providers.OrderBy(p => p.DisplayOrder)];
        return Task.FromResult(sorted);
    }

    public async Task<TileLoadResult> LoadTileAsync(IDashboardTileProvider provider, CancellationToken ct = default)
    {
        try
        {
            var data = await provider.GetTileDataAsync(ct);
            return new TileLoadResult(provider, data, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Tile {TileId} ({Title}) failed to load; showing error state",
                provider.TileId, provider.Title);
            return new TileLoadResult(provider, null, ex);
        }
    }
}
