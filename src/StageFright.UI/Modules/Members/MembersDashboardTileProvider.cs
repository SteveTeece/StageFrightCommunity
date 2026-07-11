using StageFright.Plugins.Contracts;

namespace StageFright.UI.Modules.Members;

/// <summary>
/// Dashboard tile provider for the Members module (design 3a).
/// The tile body (MembersTile) loads and renders its own data, so this provider
/// returns static TileData; DisplayOrder=10 places it first among core tiles.
/// </summary>
public class MembersDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "members";
    public string Title => "Members";
    public string ModuleName => "Members";
    public int DisplayOrder => 10;
    public string? NavigateRoute => "/members";
    public string? ActionText => "View Members";
    public Type TileComponentType => typeof(MembersTile);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData { NavigateRoute = NavigateRoute });
}
