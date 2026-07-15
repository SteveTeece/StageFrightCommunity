using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Rehearsals;

/// <summary>
/// Dashboard tile provider for the attendance trend chart (design 3a).
/// No navigation route or action link — the tile is purely informational.
/// DisplayOrder=60 places it last among core tiles.
/// </summary>
public class AttendanceTrendDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "rehearsals-attendance-trend";
    public string Title => "Attendance trend";
    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 60;
    public Type TileComponentType => typeof(AttendanceTrendTile);
    public DashboardTileSize TileSize => DashboardTileSize.OneByTwo;

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData());
}
