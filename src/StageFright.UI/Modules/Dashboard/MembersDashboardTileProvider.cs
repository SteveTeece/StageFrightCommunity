using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Plugins.Contracts;
using StageFright.UI.Shared;

namespace StageFright.UI.Modules.Dashboard;

/// <summary>
/// Dashboard tile for the Members module.
/// Displays active and inactive member counts. DisplayOrder=10 places it first among core tiles.
/// Placed in StageFright.UI because TileComponentType must reference a Blazor component.
/// </summary>
public class MembersDashboardTileProvider : IDashboardTileProvider
{
    private readonly IMemberService _memberService;

    public MembersDashboardTileProvider(IMemberService memberService)
    {
        _memberService = memberService;
    }

    public string TileId => "members";
    public string Title => "Members";
    public string ModuleName => "Members";
    public int DisplayOrder => 10;
    public Type TileComponentType => typeof(MembersTileContent);

    public async Task<TileData> GetTileDataAsync(CancellationToken ct)
    {
        var active = await _memberService.GetByStatusAsync(MemberStatus.Active, ct);
        var inactive = await _memberService.GetByStatusAsync(MemberStatus.Inactive, ct);

        return new TileData
        {
            Metrics = new Dictionary<string, string>
            {
                { "Active Members", active.Count.ToString() },
                { "Inactive Members", inactive.Count.ToString() }
            },
            NavigateRoute = "/members"
        };
    }
}
