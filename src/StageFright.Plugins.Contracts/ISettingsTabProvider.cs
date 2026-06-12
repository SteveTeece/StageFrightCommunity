namespace StageFright.Plugins.Contracts;

/// <summary>
/// Implemented by core modules and external plugins to contribute a tab to the Settings page.
/// Core tabs: General (0), Categories (10), Event Types (20), Backup (30), Restore (40).
/// Duplicate TabKeys are skipped and logged. A failing tab provider is skipped gracefully.
/// </summary>
public interface ISettingsTabProvider
{
    /// <summary>Tab title shown in the tab strip.</summary>
    string TabTitle { get; }

    /// <summary>Icon name or CSS class for the tab (e.g., a Radzen icon name).</summary>
    string TabIcon { get; }

    /// <summary>
    /// Unique key used for deep-linking: /settings?tab={TabKey}.
    /// Duplicate keys are skipped with a warning log.
    /// </summary>
    string TabKey { get; }

    /// <summary>Sort order. Core tabs: 0–99. Plugin tabs: 100+.</summary>
    int DisplayOrder { get; }

    /// <summary>Blazor component type that owns the tab content, validation, and save/cancel.</summary>
    Type SettingsComponentType { get; }
}
