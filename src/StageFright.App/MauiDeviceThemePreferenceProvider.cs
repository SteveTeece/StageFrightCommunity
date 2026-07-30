using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.App;

/// <summary>
/// Reads the OS/device's light-or-dark preference via MAUI's Application.Current.RequestedTheme.
/// </summary>
public class MauiDeviceThemePreferenceProvider : IDeviceThemePreferenceProvider
{
    public PlatformThemePreference GetPreference() => Application.Current?.RequestedTheme switch
    {
        AppTheme.Light => PlatformThemePreference.Light,
        AppTheme.Dark => PlatformThemePreference.Dark,
        _ => PlatformThemePreference.Unspecified
    };
}
