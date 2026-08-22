namespace StageFright.Plugins.Contracts;

/// <summary>
/// Implemented by external plugins to contribute a tab to the Settings page. In practice no
/// core module implements this — the built-in tabs (General, GST / BAS, Event Types,
/// Backup &amp; Restore, Committee) are hardcoded directly in SettingsPage.razor, not
/// contributed through this interface. This contract exists solely for plugin-added tabs,
/// rendered after the hardcoded ones via SettingsPage.razor's plugin-tab loop.
/// Duplicate TabKeys are skipped and logged. A failing tab provider is skipped gracefully.
/// </summary>
public interface ISettingsTabProvider
{
    /// <summary>Tab title shown in the tab strip.</summary>
    string TabTitle { get; }

    /// <summary>
    /// Icon name or CSS class for the tab (e.g., a Radzen icon name). Not currently rendered —
    /// SettingsPage.razor's plugin-tab loop only consumes TabTitle for the tab strip.
    /// </summary>
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
