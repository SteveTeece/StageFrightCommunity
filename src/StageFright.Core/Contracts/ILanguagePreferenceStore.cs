namespace StageFright.Core.Contracts;

/// <summary>
/// Reads/writes the no-database display-language preference (spec 029, FR-003/FR-006 step 2).
/// Platform-backed (MAUI Preferences); never throws — a read/write failure is caught and
/// swallowed by the implementation, matching <see cref="ISystemCultureProvider"/>/
/// <see cref="IDeviceThemePreferenceProvider"/>.
/// </summary>
public interface ILanguagePreferenceStore
{
    /// <summary>The recorded BCP-47 culture code, or null when none has been recorded or the store is unreadable.</summary>
    string? Get();

    /// <summary>Records <paramref name="cultureCode"/> as the current preference. Overwrites any prior value.</summary>
    void Set(string cultureCode);
}
