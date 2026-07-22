namespace StageFright.Core.Enums;

/// <summary>
/// The OS/device's own light-or-dark preference, as reported by the host platform.
/// Mirrors MAUI's AppTheme without StageFright.Core taking a MAUI dependency.
/// </summary>
public enum PlatformThemePreference
{
    /// <summary>The platform did not report a preference (or none is available).</summary>
    Unspecified,

    /// <summary>The platform requests a light theme.</summary>
    Light,

    /// <summary>The platform requests a dark theme.</summary>
    Dark
}
