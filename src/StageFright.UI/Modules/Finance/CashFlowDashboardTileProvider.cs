using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile provider for the six-month cash-flow chart (design 3a).
/// No navigation route or action link — the tile is purely informational.
/// DisplayOrder=50 places it after Finance (40).
/// </summary>
public class CashFlowDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "finance-cashflow";
    public string Title => "Cash flow · 6 months";
    public string ModuleName => "Finance";
    public int DisplayOrder => 50;
    public Type TileComponentType => typeof(CashFlowTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData());
}
