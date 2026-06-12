using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for the Settings singleton.
/// GetAsync returns null before first-run setup is completed.
/// SaveAsync creates or updates the single Settings row.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>Returns the Settings singleton, or null if first-run setup has not been completed.</summary>
    Task<Settings?> GetAsync(CancellationToken ct = default);

    /// <summary>Creates or updates the Settings singleton row.</summary>
    Task SaveAsync(Settings settings, CancellationToken ct = default);
}
