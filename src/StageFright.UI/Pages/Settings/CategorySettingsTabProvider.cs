using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Settings;

/// <summary>
/// Contributes the Categories tab to the Settings page.
/// DisplayOrder=10 places it after General (0) and before Event Types (20).
/// </summary>
public class CategorySettingsTabProvider : ISettingsTabProvider
{
    public string TabTitle => "Categories";
    public string TabIcon => "category";
    public string TabKey => "categories";
    public int DisplayOrder => 10;
    public Type SettingsComponentType => typeof(SettingsCategoryTab);
}
