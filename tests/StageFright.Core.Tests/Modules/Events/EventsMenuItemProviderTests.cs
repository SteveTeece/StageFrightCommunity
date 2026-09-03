using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Events;

/// <summary>
/// Unit tests for EventsMenuItemProvider: confirms the AGM sub-items were added
/// (FR-001/FR-015) alongside the existing "All Events" entry.
/// </summary>
public class EventsMenuItemProviderTests
{
    private readonly EventsMenuItemProvider _sut = new(RealStringLocalizer.For<NavigationResource>());

    [Fact]
    public void GetMenuItems_Includes_AllEvents_RoutingToEventsList()
    {
        var eventsItem = Assert.Single(_sut.GetMenuItems());

        var allEvents = Assert.Single(eventsItem.SubItems, item => item.Title == "All Events");
        Assert.Equal("/events", allEvents.Route);
    }

    [Fact]
    public void GetMenuItems_Includes_ScheduleAgm_RoutingToNewAgmForm()
    {
        var eventsItem = Assert.Single(_sut.GetMenuItems());

        var scheduleAgm = Assert.Single(eventsItem.SubItems, item => item.Title == "Schedule AGM");
        Assert.Equal("/events/agm/new", scheduleAgm.Route);
    }

    [Fact]
    public void GetMenuItems_Includes_Agms_RoutingToAgmList()
    {
        var eventsItem = Assert.Single(_sut.GetMenuItems());

        var agms = Assert.Single(eventsItem.SubItems, item => item.Title == "AGMs");
        Assert.Equal("/events/agm", agms.Route);
    }
}
