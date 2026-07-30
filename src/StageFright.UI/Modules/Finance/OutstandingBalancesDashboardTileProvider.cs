using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile provider for outstanding member fee balances (design 4).
/// The tile body (OutstandingBalancesTile) loads and renders its own data, so this
/// provider returns static TileData; DisplayOrder=45 places it between Finance (40)
/// and Cash flow (50). Renders at TwoByTwo so the trend chart has room to be legible.
/// NavigateRoute points at the Finance "Outstanding" tab (MemberBalanceList) rather than
/// the Member Account Summary report: both the tile's member count and that tab query
/// IMemberBalanceService.GetAllMemberBalancesAsync(), so the two always agree. The report
/// intentionally lists every member (including $0 balances, per spec 005) and would show a
/// larger, mismatched count if linked here instead (issue #242).
/// </summary>
public class OutstandingBalancesDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "finance-outstanding-balances";
    public string Title => "Outstanding Balances";
    public string ModuleName => "Finance";
    public int DisplayOrder => 45;
    public string? NavigateRoute => "/finance?tab=outstanding";
    public string? ActionText => "View Members";
    public Type TileComponentType => typeof(OutstandingBalancesTile);
    public DashboardTileSize TileSize => DashboardTileSize.TwoByTwo;

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
