using Microsoft.Extensions.Localization;
using StageFright.Plugins.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Modules.Rehearsals;

/// <summary>
/// Dashboard tile provider for the Rehearsals module (design 3a).
/// The tile body (RehearsalsTile) loads and renders its own data, so this provider
/// returns static TileData; DisplayOrder=20 places it after Members (10).
/// </summary>
public class RehearsalsDashboardTileProvider : IDashboardTileProvider
{
    private readonly IStringLocalizer<RehearsalsResource> _localizer;

    public RehearsalsDashboardTileProvider(IStringLocalizer<RehearsalsResource> localizer)
    {
        _localizer = localizer;
    }

    public string TileId => "rehearsals";
    public string Title => _localizer["Rehearsals_Tile_Title"];
    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 20;
    public string? NavigateRoute => "/rehearsals";
    public string? ActionText => _localizer["Rehearsals_Tile_ActionText"];
    public Type TileComponentType => typeof(RehearsalsTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
