using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Contributes the Members navigation section: top-level "/members" with sub-items.
/// DisplayOrder=1 places Members after Dashboard (0) and before other modules.
/// </summary>
public class MemberMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Members";
    public int DisplayOrder => 1;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = "Members",
            Route = "/members",
            DisplayOrder = 0,
            SubItems =
            [
                new MenuItem { Title = "Active Members", Route = "/members", DisplayOrder = 0 },
                new MenuItem { Title = "Add Member", Route = "/members/new", DisplayOrder = 1 }
            ]
        }
    ];
}
