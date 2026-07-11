using StageFright.Plugins.Contracts;
using StageFright.UI.Modules.Finance;

namespace StageFright.UI.Tests.Modules.Finance;

/// <summary>Unit tests for OutstandingBalancesDashboardTileProvider metadata and TileData.</summary>
public class OutstandingBalancesDashboardTileProviderTests
{
    private readonly IDashboardTileProvider _provider = new OutstandingBalancesDashboardTileProvider();

    [Fact]
    public void Should_ExposeOutstandingBalancesTileMetadata_When_Constructed()
    {
        Assert.Equal("finance-outstanding-balances", _provider.TileId);
        Assert.Equal("Outstanding Balances", _provider.Title);
        Assert.Equal("Finance", _provider.ModuleName);
        Assert.Equal(45, _provider.DisplayOrder);
        Assert.Equal("/reports/member-account-summary", _provider.NavigateRoute);
        Assert.Equal("View Report", _provider.ActionText);
        Assert.Equal(typeof(OutstandingBalancesTile), _provider.TileComponentType);
    }

    [Fact]
    public async Task Should_ReturnTileDataWithRoute_When_GettingTileData()
    {
        var data = await _provider.GetTileDataAsync(CancellationToken.None);

        Assert.Equal("/reports/member-account-summary", data.NavigateRoute);
    }
}
