using StageFright.Core.Contracts;
using StageFright.Plugins.Contracts;
using StageFright.UI.Shared;

namespace StageFright.UI.Modules.Dashboard;

/// <summary>
/// Dashboard tile for the Rehearsals module.
/// Displays the most recent past rehearsal date and stored attendance rate.
/// DisplayOrder=20 places it second among core tiles (after Members=10).
/// Placed in StageFright.UI because TileComponentType references a Blazor component.
/// </summary>
public class RehearsalsDashboardTileProvider : IDashboardTileProvider
{
    private readonly IRehearsalService _rehearsalService;

    public RehearsalsDashboardTileProvider(IRehearsalService rehearsalService)
    {
        _rehearsalService = rehearsalService;
    }

    public string TileId => "rehearsals";
    public string Title => "Rehearsals";
    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 20;
    public Type TileComponentType => typeof(RehearsalsTileContent);

    public async Task<TileData> GetTileDataAsync(CancellationToken ct)
    {
        var recent = await _rehearsalService.GetMostRecentPastWithoutAttendanceAsync(DateTime.UtcNow, ct);

        return new TileData
        {
            Metrics = new Dictionary<string, string>
            {
                { "Last Rehearsal", recent?.Date.ToString("d MMM yyyy") ?? "None" },
                { "Attendance Rate", recent?.StoredAttendanceRate.HasValue == true
                    ? $"{recent.StoredAttendanceRate:F1}%"
                    : "—" }
            },
            NavigateRoute = "/rehearsals"
        };
    }
}
