using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Settings;

/// <summary>
/// Contributes the General tab to the Settings page.
/// DisplayOrder=0 places it first before all other tabs.
/// </summary>
public class GeneralSettingsTabProvider : ISettingsTabProvider
{
    public string TabTitle => "General";
    public string TabIcon => "settings";
    public string TabKey => "general";
    public int DisplayOrder => 0;
    public Type SettingsComponentType => typeof(GeneralSettingsTab);
}
