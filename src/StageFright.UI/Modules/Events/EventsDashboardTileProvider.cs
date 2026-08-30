using Microsoft.Extensions.Localization;
using StageFright.Plugins.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Modules.Events;

/// <summary>
/// Dashboard tile provider for the Events module (design 3a).
/// The tile body (EventsTile) loads and renders its own data, so this provider
/// returns static TileData; DisplayOrder=30 places it after Rehearsals (20).
/// </summary>
public class EventsDashboardTileProvider : IDashboardTileProvider
{
    private readonly IStringLocalizer<EventsResource> _localizer;

    public EventsDashboardTileProvider(IStringLocalizer<EventsResource> localizer)
    {
        _localizer = localizer;
    }

    public string TileId => "events";
    public string Title => _localizer["Events_Tile_Title"];
    public string ModuleName => "Events";
    public int DisplayOrder => 30;
    public string? NavigateRoute => "/events";
    public string? ActionText => _localizer["Events_Tile_ActionText"];
    public Type TileComponentType => typeof(EventsTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
