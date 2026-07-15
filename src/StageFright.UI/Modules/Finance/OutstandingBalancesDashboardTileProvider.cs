using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile provider for outstanding member fee balances (design 4).
/// The tile body (OutstandingBalancesTile) loads and renders its own data, so this
/// provider returns static TileData; DisplayOrder=45 places it between Finance (40)
/// and Cash flow (50). Renders at TwoByTwo so the trend chart has room to be legible.
/// </summary>
public class OutstandingBalancesDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "finance-outstanding-balances";
    public string Title => "Outstanding Balances";
    public string ModuleName => "Finance";
    public int DisplayOrder => 45;
    public string? NavigateRoute => "/reports/member-account-summary";
    public string? ActionText => "View Report";
    public Type TileComponentType => typeof(OutstandingBalancesTile);
    public DashboardTileSize TileSize => DashboardTileSize.TwoByTwo;

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
