using StageFright.Core.Modules.Events;

namespace StageFright.Core.Tests.Modules.Events;

/// <summary>
/// Unit tests for EventsMenuItemProvider: confirms the AGM sub-items were added
/// (FR-001/FR-015) alongside the existing "All Events" entry.
/// </summary>
public class EventsMenuItemProviderTests
{
    private readonly EventsMenuItemProvider _sut = new();

    [Fact]
    public void GetMenuItems_Includes_AllEvents_RoutingToEventsList()
    {
        var eventsItem = Assert.Single(_sut.GetMenuItems());

        var allEvents = Assert.Single(eventsItem.SubItems, item => item.Title == "All Events");
        Assert.Equal("/events", allEvents.Route);
    }

    [Fact]
    public void GetMenuItems_Includes_RecordAgm_RoutingToNewAgmForm()
    {
        var eventsItem = Assert.Single(_sut.GetMenuItems());

        var recordAgm = Assert.Single(eventsItem.SubItems, item => item.Title == "Record AGM");
        Assert.Equal("/events/agm/new", recordAgm.Route);
    }

    [Fact]
    public void GetMenuItems_Includes_PastAgms_RoutingToAgmList()
    {
        var eventsItem = Assert.Single(_sut.GetMenuItems());

        var pastAgms = Assert.Single(eventsItem.SubItems, item => item.Title == "Past AGMs");
        Assert.Equal("/events/agm", pastAgms.Route);
    }
}
