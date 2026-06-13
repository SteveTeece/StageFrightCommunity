using StageFright.Core.Modules.Dashboard;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Contracts;

/// <summary>
/// Aggregates IDashboardTileProvider registrations and loads their data with per-tile isolation.
/// Core tiles use DisplayOrder 0–99; plugin tiles use 100+.
/// </summary>
public interface IDashboardService
{
    /// <summary>Returns all registered tile providers sorted by DisplayOrder.</summary>
    Task<IReadOnlyList<IDashboardTileProvider>> GetTilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads tile data for a single provider. Any exception from GetTileDataAsync is caught
    /// and returned as an error result — it never propagates to the caller.
    /// </summary>
    Task<TileLoadResult> LoadTileAsync(IDashboardTileProvider provider, CancellationToken ct = default);
}
