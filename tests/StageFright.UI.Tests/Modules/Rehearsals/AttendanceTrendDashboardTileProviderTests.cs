using StageFright.Plugins.Contracts;
using StageFright.UI.Modules.Rehearsals;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Modules.Rehearsals;

/// <summary>Unit tests for AttendanceTrendDashboardTileProvider metadata and TileData.</summary>
public class AttendanceTrendDashboardTileProviderTests
{
    private readonly IDashboardTileProvider _provider = new AttendanceTrendDashboardTileProvider(RealStringLocalizer.For<RehearsalsResource>());

    [Fact]
    public void Should_ExposeTrendTileMetadata_When_Constructed()
    {
        Assert.Equal("rehearsals-attendance-trend", _provider.TileId);
        Assert.Equal("Attendance trend", _provider.Title);
        Assert.Equal("Rehearsals", _provider.ModuleName);
        Assert.Equal(60, _provider.DisplayOrder);
        Assert.Null(_provider.NavigateRoute);
        Assert.Null(_provider.ActionText);
        Assert.Equal(typeof(AttendanceTrendTile), _provider.TileComponentType);
        Assert.Equal(DashboardTileSize.OneByTwo, _provider.TileSize);
    }

    [Fact]
    public async Task Should_ReturnTileDataWithoutRoute_When_GettingTileData()
    {
        var data = await _provider.GetTileDataAsync(CancellationToken.None);

        Assert.Null(data.NavigateRoute);
    }
}
