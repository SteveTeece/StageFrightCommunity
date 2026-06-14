using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Settings;

/// <summary>
/// Contributes the Event Types tab to the Settings page.
/// DisplayOrder=20 places it after Categories (10).
/// </summary>
public class EventTypesSettingsTabProvider : ISettingsTabProvider
{
    public string TabTitle => "Event Types";
    public string TabIcon => "event";
    public string TabKey => "event-types";
    public int DisplayOrder => 20;
    public Type SettingsComponentType => typeof(EventTypesTab);
}
