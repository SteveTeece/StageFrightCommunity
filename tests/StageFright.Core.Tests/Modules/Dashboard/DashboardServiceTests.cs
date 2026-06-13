using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Modules.Dashboard;
using StageFright.Core.Tests.Fixtures;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Tests.Modules.Dashboard;

/// <summary>
/// Unit tests for DashboardService — provider ordering, parallel load isolation,
/// and error containment per tile.
/// </summary>
public class DashboardServiceTests : TestBase
{
    private static IDashboardTileProvider MakeProvider(string id, int displayOrder)
    {
        var p = Substitute.For<IDashboardTileProvider>();
        p.TileId.Returns(id);
        p.Title.Returns(id);
        p.DisplayOrder.Returns(displayOrder);
        p.GetTileDataAsync(Arg.Any<CancellationToken>())
            .Returns(new TileData { Metrics = new Dictionary<string, string> { { "k", "v" } } });
        return p;
    }

    private static DashboardService Build(params IDashboardTileProvider[] providers) =>
        new(providers, NullLogger<DashboardService>.Instance);

    // --- GetTilesAsync ordering ---

    [Fact]
    public async Task GetTilesAsync_ReturnsProviders_SortedByDisplayOrder()
    {
        var high = MakeProvider("high", 50);
        var low = MakeProvider("low", 10);
        var plugin = MakeProvider("plugin", 100);
        var sut = Build(high, low, plugin);

        var result = await sut.GetTilesAsync(Ct);

        Assert.Equal(["low", "high", "plugin"], result.Select(p => p.TileId).ToArray());
    }

    [Fact]
    public async Task GetTilesAsync_EmptyProviders_ReturnsEmptyList()
    {
        var sut = Build();

        var result = await sut.GetTilesAsync(Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTilesAsync_CoreProviders_HaveDisplayOrderUnder100()
    {
        var sut = Build(MakeProvider("members", 10), MakeProvider("rehearsals", 20), MakeProvider("finance", 40));

        var result = await sut.GetTilesAsync(Ct);

        Assert.All(result, p => Assert.True(p.DisplayOrder < 100));
    }

    [Fact]
    public async Task GetTilesAsync_PluginProvider_HasDisplayOrder100OrMore()
    {
        var sut = Build(MakeProvider("core", 10), MakeProvider("plugin", 100));

        var result = await sut.GetTilesAsync(Ct);

        Assert.Single(result, p => p.DisplayOrder >= 100);
    }

    // --- LoadTileAsync success ---

    [Fact]
    public async Task LoadTileAsync_SuccessfulProvider_ReturnsTileData()
    {
        var provider = MakeProvider("members", 10);
        var sut = Build(provider);

        var result = await sut.LoadTileAsync(provider, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task LoadTileAsync_SuccessfulProvider_ReturnsCorrectProvider()
    {
        var provider = MakeProvider("members", 10);
        var sut = Build(provider);

        var result = await sut.LoadTileAsync(provider, Ct);

        Assert.Same(provider, result.Provider);
    }

    // --- LoadTileAsync isolation ---

    [Fact]
    public async Task LoadTileAsync_ThrowingProvider_ReturnsErrorResult_DoesNotThrow()
    {
        var provider = Substitute.For<IDashboardTileProvider>();
        provider.TileId.Returns("bad");
        provider.Title.Returns("Bad Tile");
        provider.DisplayOrder.Returns(10);
        provider.GetTileDataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service down"));

        var sut = Build(provider);

        var result = await sut.LoadTileAsync(provider, Ct);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task LoadTileAsync_OneThrowingProvider_DoesNotAffectOtherLoads()
    {
        var goodProvider = MakeProvider("good", 10);
        var badProvider = Substitute.For<IDashboardTileProvider>();
        badProvider.TileId.Returns("bad");
        badProvider.Title.Returns("Bad");
        badProvider.DisplayOrder.Returns(20);
        badProvider.GetTileDataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("boom"));

        var sut = Build(goodProvider, badProvider);

        var goodResult = await sut.LoadTileAsync(goodProvider, Ct);
        var badResult = await sut.LoadTileAsync(badProvider, Ct);

        Assert.True(goodResult.IsSuccess);
        Assert.False(badResult.IsSuccess);
    }

    [Fact]
    public async Task LoadTileAsync_ParallelLoads_AllComplete()
    {
        var p1 = MakeProvider("p1", 10);
        var p2 = MakeProvider("p2", 20);
        var p3 = MakeProvider("p3", 30);
        var sut = Build(p1, p2, p3);

        var tasks = new[] { p1, p2, p3 }
            .Select(p => sut.LoadTileAsync(p, Ct))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(3, results.Length);
        Assert.All(results, r => Assert.True(r.IsSuccess));
    }

    // --- Ordering boundary ---

    [Fact]
    public async Task GetTilesAsync_SortsByDisplayOrder_DashboardAlwaysFirst()
    {
        var dashboard = MakeProvider("dashboard", 0);
        var members = MakeProvider("members", 10);
        var sut = Build(members, dashboard);

        var result = await sut.GetTilesAsync(Ct);

        Assert.Equal("dashboard", result[0].TileId);
        Assert.Equal("members", result[1].TileId);
    }
}
