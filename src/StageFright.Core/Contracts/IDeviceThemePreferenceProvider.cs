using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>Reads the host platform's light/dark theme preference.</summary>
public interface IDeviceThemePreferenceProvider
{
    /// <summary>Returns the platform's current theme preference.</summary>
    PlatformThemePreference GetPreference();
}
