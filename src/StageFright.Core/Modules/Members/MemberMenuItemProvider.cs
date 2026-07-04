using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Contributes the top-level Members navigation item.
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
            ShortLabel = "MEMB",
            DisplayOrder = 0
        }
    ];
}
