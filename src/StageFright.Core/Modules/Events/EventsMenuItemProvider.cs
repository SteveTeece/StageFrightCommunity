using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Events;

/// <summary>
/// Contributes the Events navigation section.
/// DisplayOrder=4 places Events after Rehearsals (3) and before Finance (5).
/// </summary>
public class EventsMenuItemProvider : IMenuItemProvider
{
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public EventsMenuItemProvider(IStringLocalizer<NavigationResource> localizer)
    {
        _localizer = localizer;
    }

    public string ModuleName => "Events";
    public int DisplayOrder => 4;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = _localizer["Nav_Events_Title"],
            Route = "/events",
            ShortLabel = _localizer["Nav_Events_ShortLabel"],
            DisplayOrder = 0,
            SubItems =
            [
                new MenuItem { Title = _localizer["Nav_Events_AllEvents"], Route = "/events", DisplayOrder = 0 },
                new MenuItem { Title = _localizer["Nav_Events_ScheduleAgm"], Route = "/events/agm/new", DisplayOrder = 1 },
                new MenuItem { Title = _localizer["Nav_Events_Agms"], Route = "/events/agm", DisplayOrder = 2 }
            ]
        }
    ];
}
