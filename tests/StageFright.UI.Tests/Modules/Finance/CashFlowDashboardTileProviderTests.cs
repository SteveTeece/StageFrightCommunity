using StageFright.Plugins.Contracts;
using StageFright.UI.Modules.Finance;

namespace StageFright.UI.Tests.Modules.Finance;

/// <summary>Unit tests for CashFlowDashboardTileProvider metadata and TileData.</summary>
public class CashFlowDashboardTileProviderTests
{
    private readonly IDashboardTileProvider _provider = new CashFlowDashboardTileProvider();

    [Fact]
    public void Should_ExposeCashFlowTileMetadata_When_Constructed()
    {
        Assert.Equal("finance-cashflow", _provider.TileId);
        Assert.Equal("Cash flow · 6 months", _provider.Title);
        Assert.Equal("Finance", _provider.ModuleName);
        Assert.Equal(50, _provider.DisplayOrder);
        Assert.Null(_provider.NavigateRoute);
        Assert.Null(_provider.ActionText);
        Assert.Equal(typeof(CashFlowTile), _provider.TileComponentType);
        Assert.Equal(DashboardTileSize.TwoByOne, _provider.TileSize);
    }

    [Fact]
    public async Task Should_ReturnTileDataWithoutRoute_When_GettingTileData()
    {
        var data = await _provider.GetTileDataAsync(CancellationToken.None);

        Assert.Null(data.NavigateRoute);
    }
}
