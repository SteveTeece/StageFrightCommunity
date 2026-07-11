using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile provider for the Finance module (design 3a).
/// The tile body (FinanceTile) loads and renders its own data, so this provider
/// returns static TileData; DisplayOrder=40 places it after Events (30).
/// </summary>
public class FinanceDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "finance";
    public string Title => "Finance";
    public string ModuleName => "Finance";
    public int DisplayOrder => 40;
    public string? NavigateRoute => "/finance";
    public string? ActionText => "Open Finance";
    public Type TileComponentType => typeof(FinanceTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
