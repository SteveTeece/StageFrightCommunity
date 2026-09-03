using StageFright.UI.Modules.Rehearsals;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Modules.Rehearsals;

/// <summary>Unit tests for RehearsalsDashboardTileProvider metadata and TileData.</summary>
public class RehearsalsDashboardTileProviderTests
{
    private readonly RehearsalsDashboardTileProvider _provider = new(RealStringLocalizer.For<RehearsalsResource>());

    [Fact]
    public void Should_ExposeRehearsalsTileMetadata_When_Constructed()
    {
        Assert.Equal("rehearsals", _provider.TileId);
        Assert.Equal("Rehearsals", _provider.Title);
        Assert.Equal("Rehearsals", _provider.ModuleName);
        Assert.Equal(20, _provider.DisplayOrder);
        Assert.Equal("/rehearsals", _provider.NavigateRoute);
        Assert.Equal("View Rehearsals", _provider.ActionText);
        Assert.Equal(typeof(RehearsalsTile), _provider.TileComponentType);
    }

    [Fact]
    public async Task Should_ReturnTileDataWithRoute_When_GettingTileData()
    {
        var data = await _provider.GetTileDataAsync(CancellationToken.None);

        Assert.Equal("/rehearsals", data.NavigateRoute);
    }
}
