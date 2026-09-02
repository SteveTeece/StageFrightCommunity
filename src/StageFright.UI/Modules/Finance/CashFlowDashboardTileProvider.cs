using Microsoft.Extensions.Localization;
using StageFright.Plugins.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile provider for the six-month cash-flow chart (design 3a).
/// No navigation route or action link — the tile is purely informational.
/// DisplayOrder=50 places it after Finance (40). Renders at TwoByTwo so the
/// chart has room to be legible.
/// </summary>
public class CashFlowDashboardTileProvider : IDashboardTileProvider
{
    private readonly IStringLocalizer<FinanceResource> _localizer;

    public CashFlowDashboardTileProvider(IStringLocalizer<FinanceResource> localizer)
    {
        _localizer = localizer;
    }

    public string TileId => "finance-cashflow";
    public string Title => _localizer["Finance_CashFlowTile_Title"];
    public string ModuleName => "Finance";
    public int DisplayOrder => 50;
    public Type TileComponentType => typeof(CashFlowTile);
    public DashboardTileSize TileSize => DashboardTileSize.TwoByTwo;

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData());
}
