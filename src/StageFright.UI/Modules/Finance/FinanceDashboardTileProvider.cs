using Microsoft.Extensions.Localization;
using StageFright.Plugins.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile provider for the Finance module (design 3a).
/// The tile body (FinanceTile) loads and renders its own data, so this provider
/// returns static TileData; DisplayOrder=40 places it after Events (30).
/// </summary>
public class FinanceDashboardTileProvider : IDashboardTileProvider
{
    private readonly IStringLocalizer<FinanceResource> _localizer;

    public FinanceDashboardTileProvider(IStringLocalizer<FinanceResource> localizer)
    {
        _localizer = localizer;
    }

    public string TileId => "finance";
    public string Title => _localizer["Finance_Tile_Title"];
    public string ModuleName => "Finance";
    public int DisplayOrder => 40;
    public string? NavigateRoute => "/finance";
    public string? ActionText => _localizer["Finance_Tile_ActionText"];
    public Type TileComponentType => typeof(FinanceTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
