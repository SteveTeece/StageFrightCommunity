using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Contributes the top-level Members navigation item.
/// DisplayOrder=1 places Members after Dashboard (0) and before other modules.
/// </summary>
public class MemberMenuItemProvider : IMenuItemProvider
{
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public MemberMenuItemProvider(IStringLocalizer<NavigationResource> localizer)
    {
        _localizer = localizer;
    }

    public string ModuleName => "Members";
    public int DisplayOrder => 1;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = _localizer["Nav_Members_Title"],
            Route = "/members",
            ShortLabel = _localizer["Nav_Members_ShortLabel"],
            DisplayOrder = 0
        }
    ];
}
