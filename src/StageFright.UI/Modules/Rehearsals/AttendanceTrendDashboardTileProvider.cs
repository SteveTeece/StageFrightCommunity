using Microsoft.Extensions.Localization;
using StageFright.Plugins.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Modules.Rehearsals;

/// <summary>
/// Dashboard tile provider for the attendance trend chart (design 3a).
/// No navigation route or action link — the tile is purely informational.
/// DisplayOrder=60 places it last among core tiles.
/// </summary>
public class AttendanceTrendDashboardTileProvider : IDashboardTileProvider
{
    private readonly IStringLocalizer<RehearsalsResource> _localizer;

    public AttendanceTrendDashboardTileProvider(IStringLocalizer<RehearsalsResource> localizer)
    {
        _localizer = localizer;
    }

    public string TileId => "rehearsals-attendance-trend";
    public string Title => _localizer["Rehearsals_TrendTile_Title"];
    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 60;
    public Type TileComponentType => typeof(AttendanceTrendTile);
    public DashboardTileSize TileSize => DashboardTileSize.OneByTwo;

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData());
}
