using StageFright.UI.Modules.Members;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Modules.Members;

/// <summary>Unit tests for MembersDashboardTileProvider metadata and TileData.</summary>
public class MembersDashboardTileProviderTests
{
    private readonly MembersDashboardTileProvider _provider =
        new(RealStringLocalizer.For<MembersResource>());

    [Fact]
    public void Should_ExposeMembersTileMetadata_When_Constructed()
    {
        Assert.Equal("members", _provider.TileId);
        Assert.Equal("Members", _provider.Title);
        Assert.Equal("Members", _provider.ModuleName);
        Assert.Equal(10, _provider.DisplayOrder);
        Assert.Equal("/members", _provider.NavigateRoute);
        Assert.Equal("View Members", _provider.ActionText);
        Assert.Equal(typeof(MembersTile), _provider.TileComponentType);
    }

    [Fact]
    public async Task Should_ReturnTileDataWithRoute_When_GettingTileData()
    {
        var data = await _provider.GetTileDataAsync(CancellationToken.None);

        Assert.Equal("/members", data.NavigateRoute);
    }
}
