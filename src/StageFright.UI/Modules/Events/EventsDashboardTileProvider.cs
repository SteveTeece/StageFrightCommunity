using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Events;

/// <summary>
/// Dashboard tile provider for the Events module (design 3a).
/// The tile body (EventsTile) loads and renders its own data, so this provider
/// returns static TileData; DisplayOrder=30 places it after Rehearsals (20).
/// </summary>
public class EventsDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "events";
    public string Title => "Events";
    public string ModuleName => "Events";
    public int DisplayOrder => 30;
    public string? NavigateRoute => "/events";
    public string? ActionText => "View Events";
    public Type TileComponentType => typeof(EventsTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
