using StageFright.UI.Modules.Events;

namespace StageFright.UI.Tests.Modules.Events;

/// <summary>Unit tests for EventsDashboardTileProvider metadata and TileData.</summary>
public class EventsDashboardTileProviderTests
{
    private readonly EventsDashboardTileProvider _provider = new();

    [Fact]
    public void Should_ExposeEventsTileMetadata_When_Constructed()
    {
        Assert.Equal("events", _provider.TileId);
        Assert.Equal("Events", _provider.Title);
        Assert.Equal("Events", _provider.ModuleName);
        Assert.Equal(30, _provider.DisplayOrder);
        Assert.Equal("/events", _provider.NavigateRoute);
        Assert.Equal("View Events", _provider.ActionText);
        Assert.Equal(typeof(EventsTile), _provider.TileComponentType);
    }

    [Fact]
    public async Task Should_ReturnTileDataWithRoute_When_GettingTileData()
    {
        var data = await _provider.GetTileDataAsync(CancellationToken.None);

        Assert.Equal("/events", data.NavigateRoute);
    }
}
