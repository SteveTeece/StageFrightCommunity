using StageFright.Core.Contracts;
using StageFright.Plugins.Contracts;
using StageFright.UI.Shared;

namespace StageFright.UI.Modules.Dashboard;

/// <summary>
/// Dashboard tile for the Events module.
/// Displays the most recent past event date and stored participation rate.
/// DisplayOrder=30 places it third among core tiles (after Rehearsals=20).
/// Placed in StageFright.UI because TileComponentType references a Blazor component.
/// </summary>
public class EventsDashboardTileProvider : IDashboardTileProvider
{
    private readonly IEventService _eventService;

    public EventsDashboardTileProvider(IEventService eventService)
    {
        _eventService = eventService;
    }

    public string TileId => "events";
    public string Title => "Events";
    public string ModuleName => "Events";
    public int DisplayOrder => 30;
    public string? NavigateRoute => "/events";
    public Type TileComponentType => typeof(EventsTileContent);

    public async Task<TileData> GetTileDataAsync(CancellationToken ct)
    {
        var recent = await _eventService.GetMostRecentPastWithoutParticipationAsync(DateTime.UtcNow, ct);

        return new TileData
        {
            Metrics = new Dictionary<string, string>
            {
                { "Last Event", recent?.Date.ToString("d MMM yyyy") ?? "None" },
                { "Participation Rate", recent?.StoredParticipationRate.HasValue == true
                    ? $"{recent.StoredParticipationRate:F1}%"
                    : "—" }
            },
            NavigateRoute = "/events"
        };
    }
}
