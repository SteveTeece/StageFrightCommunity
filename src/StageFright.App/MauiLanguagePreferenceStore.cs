using StageFright.Core.Contracts;

namespace StageFright.App;

/// <summary>
/// Reads/writes the no-database display-language preference (spec 029, FR-003) via MAUI's
/// <see cref="Preferences"/> API, under a single fixed key. Never throws — a read/write failure
/// is caught and swallowed, matching <see cref="SystemCultureProvider"/>/
/// <see cref="MauiDeviceThemePreferenceProvider"/>'s never-throw contract.
/// </summary>
public sealed class MauiLanguagePreferenceStore : ILanguagePreferenceStore
{
    private const string PreferenceKey = "DisplayLanguageCode";

    public string? Get()
    {
        try
        {
            var value = Preferences.Default.Get<string?>(PreferenceKey, null);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public void Set(string cultureCode)
    {
        try
        {
            Preferences.Default.Set(PreferenceKey, cultureCode);
        }
        catch
        {
            // Write failure — the preference simply isn't recorded this time; never throws.
        }
    }
}
