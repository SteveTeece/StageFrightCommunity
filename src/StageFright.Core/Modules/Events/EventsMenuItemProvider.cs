using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Events;

/// <summary>
/// Contributes the Events navigation section.
/// DisplayOrder=3 places Events after Rehearsals (2) and before Finance (4).
/// </summary>
public class EventsMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Events";
    public int DisplayOrder => 3;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = "Events",
            Route = "/events",
            ShortLabel = "EVNT",
            DisplayOrder = 0
        }
    ];
}
