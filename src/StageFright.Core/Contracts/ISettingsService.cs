using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application-layer wrapper over ISettingsRepository.
/// Reads and persists the Settings singleton; audits changes.
/// </summary>
public interface ISettingsService
{
    /// <summary>Returns the current Settings, or null before first-run setup.</summary>
    Task<Settings?> GetAsync(CancellationToken ct = default);

    /// <summary>Persists the Settings singleton and writes an audit entry for changed fields.</summary>
    Task SaveAsync(Settings settings, CancellationToken ct = default);
}
