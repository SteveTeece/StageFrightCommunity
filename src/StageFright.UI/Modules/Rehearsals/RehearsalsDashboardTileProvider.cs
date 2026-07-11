using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Rehearsals;

/// <summary>
/// Dashboard tile provider for the Rehearsals module (design 3a).
/// The tile body (RehearsalsTile) loads and renders its own data, so this provider
/// returns static TileData; DisplayOrder=20 places it after Members (10).
/// </summary>
public class RehearsalsDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "rehearsals";
    public string Title => "Rehearsals";
    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 20;
    public string? NavigateRoute => "/rehearsals";
    public string? ActionText => "View Rehearsals";
    public Type TileComponentType => typeof(RehearsalsTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
