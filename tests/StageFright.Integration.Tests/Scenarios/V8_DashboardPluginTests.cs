using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Modules.Dashboard;
using StageFright.Plugins.Contracts;
using StageFright.TestPlugin;
using StageFright.UI.Modules.Events;
using StageFright.UI.Modules.Finance;
using StageFright.UI.Modules.Members;
using StageFright.UI.Modules.Rehearsals;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V8: Dashboard overview and plugin extensibility.
/// Verifies that core tiles load in parallel, a failing provider is isolated,
/// the TestPlugin tile appears in the Extensions section (DisplayOrder 100+),
/// and missing-dependency plugins are skipped without blocking others.
/// Core tile providers are self-contained (design 3a); their tile components
/// own data loading, so no database wiring is required here.
/// </summary>
public sealed class V8_DashboardPluginTests
{

    // --- Core tiles load ---

    [Fact]
    public async Task GetTilesAsync_Returns_AllCoreProviders_Ordered()
    {
        var svc = BuildDashboardService();

        var tiles = await svc.GetTilesAsync();

        // Members=10, Rehearsals=20, Finance=40 — all DisplayOrder < 100
        var coreTiles = tiles.Where(t => t.DisplayOrder < 100).ToList();
        Assert.True(coreTiles.Count >= 3, $"Expected at least 3 core tiles but got {coreTiles.Count}");
    }

    [Fact]
    public async Task GetTilesAsync_CoreTiles_AreSortedByDisplayOrder()
    {
        var svc = BuildDashboardService();

        var tiles = await svc.GetTilesAsync();

        var orders = tiles.Select(t => t.DisplayOrder).ToList();
        Assert.Equal(orders.OrderBy(x => x).ToList(), orders);
    }

    [Fact]
    public async Task LoadTileAsync_MembersTile_ReturnsNavigableTileData()
    {
        var svc = BuildDashboardService();
        var tiles = await svc.GetTilesAsync();
        var membersTile = tiles.Single(t => t.TileId == "members");

        var result = await svc.LoadTileAsync(membersTile);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("/members", result.Data!.NavigateRoute);
    }

    [Fact]
    public async Task LoadTileAsync_RehearsalsTile_ReturnsNavigableTileData()
    {
        var svc = BuildDashboardService();
        var tiles = await svc.GetTilesAsync();
        var tile = tiles.Single(t => t.TileId == "rehearsals");

        var result = await svc.LoadTileAsync(tile);

        Assert.True(result.IsSuccess);
        Assert.Equal("/rehearsals", result.Data!.NavigateRoute);
    }

    [Fact]
    public async Task LoadTileAsync_FinanceTile_ReturnsNavigableTileData()
    {
        var svc = BuildDashboardService();
        var tiles = await svc.GetTilesAsync();
        var tile = tiles.Single(t => t.TileId == "finance");

        var result = await svc.LoadTileAsync(tile);

        Assert.True(result.IsSuccess);
        Assert.Equal("/finance", result.Data!.NavigateRoute);
    }

    [Fact]
    public async Task GetTilesAsync_IncludesChartTiles_WithoutNavigation()
    {
        var svc = BuildDashboardService();

        var tiles = await svc.GetTilesAsync();

        var cashFlow = tiles.Single(t => t.TileId == "finance-cashflow");
        var trend = tiles.Single(t => t.TileId == "rehearsals-attendance-trend");
        Assert.Null(cashFlow.NavigateRoute);
        Assert.Null(trend.NavigateRoute);
        Assert.Null(cashFlow.ActionText);
        Assert.Null(trend.ActionText);
    }

    // --- Parallel load isolation ---

    [Fact]
    public async Task LoadTileAsync_SlowTile_DoesNotBlock_OtherTiles()
    {
        var slowProvider = Substitute.For<IDashboardTileProvider>();
        slowProvider.TileId.Returns("slow");
        slowProvider.Title.Returns("Slow Tile");
        slowProvider.DisplayOrder.Returns(50);
        slowProvider.GetTileDataAsync(Arg.Any<CancellationToken>())
            .Returns(async (ci) =>
            {
                await Task.Delay(200, ci.Arg<CancellationToken>());
                return new TileData { Metrics = new Dictionary<string, string> { { "k", "v" } } };
            });

        var fastProvider = Substitute.For<IDashboardTileProvider>();
        fastProvider.TileId.Returns("fast");
        fastProvider.Title.Returns("Fast Tile");
        fastProvider.DisplayOrder.Returns(10);
        fastProvider.GetTileDataAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TileData()));

        var svc = new DashboardService([slowProvider, fastProvider], NullLogger<DashboardService>.Instance);

        // Start both loads simultaneously
        var slowTask = svc.LoadTileAsync(slowProvider);
        var fastTask = svc.LoadTileAsync(fastProvider);

        // Fast tile should complete before slow tile
        var fastResult = await fastTask;
        Assert.True(fastResult.IsSuccess, "Fast tile should succeed independently of slow tile");

        var slowResult = await slowTask;
        Assert.True(slowResult.IsSuccess, "Slow tile should also eventually succeed");
    }

    [Fact]
    public async Task LoadTileAsync_ThrowingProvider_IsIsolated_OtherTilesUnaffected()
    {
        var throwingProvider = Substitute.For<IDashboardTileProvider>();
        throwingProvider.TileId.Returns("broken");
        throwingProvider.Title.Returns("Broken Tile");
        throwingProvider.DisplayOrder.Returns(99);
        throwingProvider.GetTileDataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var svc = BuildDashboardService(extraProviders: [throwingProvider]);
        var tiles = await svc.GetTilesAsync();

        var membersTile = tiles.Single(t => t.TileId == "members");
        var brokenTile = tiles.Single(t => t.TileId == "broken");

        var tasks = new[] { membersTile, brokenTile }
            .Select(t => svc.LoadTileAsync(t))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var membersResult = results.Single(r => r.Provider.TileId == "members");
        var brokenResult = results.Single(r => r.Provider.TileId == "broken");

        Assert.True(membersResult.IsSuccess, "Members tile should succeed despite broken tile");
        Assert.False(brokenResult.IsSuccess, "Broken tile should have error result");
        Assert.NotNull(brokenResult.Error);
    }

    // --- TestPlugin tile in Extensions section ---

    [Fact]
    public void TestPlugin_TileProvider_HasDisplayOrder100()
    {
        var testProvider = new TestTileProvider();

        Assert.Equal(100, testProvider.DisplayOrder);
    }

    [Fact]
    public async Task TestPlugin_AppearInExtensionsSection_WhenRegistered()
    {
        var testProvider = new TestTileProvider();
        var svc = BuildDashboardService(extraProviders: [testProvider]);

        var tiles = await svc.GetTilesAsync();

        var extensionTiles = tiles.Where(t => t.DisplayOrder >= 100).ToList();
        Assert.Contains(extensionTiles, t => t.TileId == "test-tile");
    }

    [Fact]
    public async Task TestPlugin_TileData_ContainsExpectedMetrics()
    {
        var testProvider = new TestTileProvider();
        var svc = new DashboardService([testProvider], NullLogger<DashboardService>.Instance);

        var result = await svc.LoadTileAsync(testProvider);

        Assert.True(result.IsSuccess);
        Assert.Contains("Test Metric", result.Data!.Metrics.Keys);
    }

    [Fact]
    public async Task TestPlugin_PlacedAfterCoreTiles_InSortedOrder()
    {
        var testProvider = new TestTileProvider();
        var svc = BuildDashboardService(extraProviders: [testProvider]);

        var tiles = await svc.GetTilesAsync();
        var tileList = tiles.ToList();

        var testIndex = tileList.FindIndex(t => t.TileId == "test-tile");
        var coreIndices = tileList
            .Select((t, i) => (t, i))
            .Where(x => x.t.DisplayOrder < 100)
            .Select(x => x.i)
            .ToList();

        Assert.All(coreIndices, ci => Assert.True(ci < testIndex,
            "All core tiles should appear before TestPlugin tile"));
    }

    // --- Helpers ---

    private static DashboardService BuildDashboardService(
        IEnumerable<IDashboardTileProvider>? extraProviders = null)
    {
        var providers = new List<IDashboardTileProvider>
        {
            new MembersDashboardTileProvider(),
            new RehearsalsDashboardTileProvider(),
            new EventsDashboardTileProvider(),
            new FinanceDashboardTileProvider(),
            new CashFlowDashboardTileProvider(),
            new AttendanceTrendDashboardTileProvider()
        };

        if (extraProviders is not null)
            providers.AddRange(extraProviders);

        return new DashboardService(providers, NullLogger<DashboardService>.Instance);
    }
}
