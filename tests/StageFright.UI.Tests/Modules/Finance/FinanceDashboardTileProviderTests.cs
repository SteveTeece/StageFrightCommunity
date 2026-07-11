using StageFright.UI.Modules.Finance;

namespace StageFright.UI.Tests.Modules.Finance;

/// <summary>Unit tests for FinanceDashboardTileProvider metadata and TileData.</summary>
public class FinanceDashboardTileProviderTests
{
    private readonly FinanceDashboardTileProvider _provider = new();

    [Fact]
    public void Should_ExposeFinanceTileMetadata_When_Constructed()
    {
        Assert.Equal("finance", _provider.TileId);
        Assert.Equal("Finance", _provider.Title);
        Assert.Equal("Finance", _provider.ModuleName);
        Assert.Equal(40, _provider.DisplayOrder);
        Assert.Equal("/finance", _provider.NavigateRoute);
        Assert.Equal("Open Finance", _provider.ActionText);
        Assert.Equal(typeof(FinanceTile), _provider.TileComponentType);
    }

    [Fact]
    public async Task Should_ReturnTileDataWithRoute_When_GettingTileData()
    {
        var data = await _provider.GetTileDataAsync(CancellationToken.None);

        Assert.Equal("/finance", data.NavigateRoute);
    }
}
