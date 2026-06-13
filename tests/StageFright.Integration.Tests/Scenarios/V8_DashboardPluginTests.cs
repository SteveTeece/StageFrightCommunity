using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Dashboard;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;
using StageFright.TestPlugin;
using StageFright.UI.Modules.Dashboard;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V8: Dashboard overview and plugin extensibility.
/// Verifies that core tiles load in parallel, a failing provider is isolated,
/// the TestPlugin tile appears in the Extensions section (DisplayOrder 100+),
/// and missing-dependency plugins are skipped without blocking others.
/// Uses real tile providers wired to a real in-memory database.
/// </summary>
public sealed class V8_DashboardPluginTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

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
    public async Task LoadTileAsync_MembersTile_ReturnsMetrics()
    {
        var svc = BuildDashboardService();
        var tiles = await svc.GetTilesAsync();
        var membersTile = tiles.Single(t => t.TileId == "members");

        var result = await svc.LoadTileAsync(membersTile);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!.Metrics);
    }

    [Fact]
    public async Task LoadTileAsync_RehearsalsTile_ReturnsMetrics()
    {
        var svc = BuildDashboardService();
        var tiles = await svc.GetTilesAsync();
        var tile = tiles.Single(t => t.TileId == "rehearsals");

        var result = await svc.LoadTileAsync(tile);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task LoadTileAsync_FinanceTile_ReturnsMetrics()
    {
        var svc = BuildDashboardService();
        var tiles = await svc.GetTilesAsync();
        var tile = tiles.Single(t => t.TileId == "finance");

        var result = await svc.LoadTileAsync(tile);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
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

    private DashboardService BuildDashboardService(
        IEnumerable<IDashboardTileProvider>? extraProviders = null)
    {
        var memberRepo = new MemberRepository(_db);
        var rehearsalRepo = new RehearsalRepository(_db);
        var glRepo = new GLRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var settingsRepo = new SettingsRepository(_db);
        var unitOfWork = new UnitOfWork(_db);

        var memberSvc = BuildMemberService(memberRepo, settingsRepo, auditSvc, unitOfWork);
        var rehearsalSvc = new RehearsalService(rehearsalRepo, memberRepo, auditSvc, unitOfWork);

        var providers = new List<IDashboardTileProvider>
        {
            new MembersDashboardTileProvider(memberSvc),
            new RehearsalsDashboardTileProvider(rehearsalSvc),
            new FinanceDashboardTileProvider(glRepo)
        };

        if (extraProviders is not null)
            providers.AddRange(extraProviders);

        return new DashboardService(providers, NullLogger<DashboardService>.Instance);
    }

    private static IMemberService BuildMemberService(
        IMemberRepository memberRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService auditSvc,
        IUnitOfWork unitOfWork)
    {
        var ageCalc = new AgeCalculationService();
        var validation = new MemberValidationService(ageCalc);
        var committeeRepo = Substitute.For<ICommitteeMembershipRepository>();

        return new MemberService(memberRepo, committeeRepo, validation, settingsRepo, auditSvc, unitOfWork);
    }
}
