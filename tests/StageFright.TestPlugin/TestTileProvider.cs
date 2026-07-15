using StageFright.Plugins.Contracts;

namespace StageFright.TestPlugin;

/// <summary>
/// Test-fixture tile provider for acceptance and integration tests.
/// DisplayOrder=100 places it in the Extensions section (plugin tiles use 100+).
/// </summary>
public class TestTileProvider : IDashboardTileProvider
{
    public string TileId => "test-tile";
    public string Title => "Test Tile";
    public string ModuleName => "TestPlugin";
    public int DisplayOrder => 100;
    public DashboardTileSize TileSize => DashboardTileSize.OneByTwo;

    /// <summary>
    /// Returns typeof(object) as a placeholder — the TestPlugin has no Blazor dependency.
    /// UI rendering of this tile is tested via DashboardService/acceptance tests, not UI tests.
    /// </summary>
    public Type TileComponentType => typeof(object);

    public Task<TileData> GetTileDataAsync(CancellationToken ct) =>
        Task.FromResult(new TileData
        {
            Metrics = new Dictionary<string, string>
            {
                { "Test Metric", "42" },
                { "Plugin Status", "Active" }
            },
            NavigateRoute = null
        });
}
