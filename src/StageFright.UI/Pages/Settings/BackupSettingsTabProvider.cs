using StageFright.Plugins.Contracts;

namespace StageFright.UI.Pages.Settings;

/// <summary>
/// Contributes the Backup &amp; Restore tab to the Settings page.
/// DisplayOrder=30 places it after Categories (10) and Event Types (20).
/// </summary>
public class BackupSettingsTabProvider : ISettingsTabProvider
{
    public string TabTitle => "Backup & Restore";
    public string TabIcon => "backup";
    public string TabKey => "backup";
    public int DisplayOrder => 30;
    public Type SettingsComponentType => typeof(BackupRestoreTab);
}
