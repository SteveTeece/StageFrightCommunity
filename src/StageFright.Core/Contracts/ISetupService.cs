using StageFright.Core.Modules.Settings;

namespace StageFright.Core.Contracts;

/// <summary>
/// Orchestrates first-run setup: persists Settings, audits initialization.
/// </summary>
public interface ISetupService
{
    /// <summary>Returns true when a Settings record exists (first-run setup has been completed).</summary>
    Task<bool> IsSetupCompleteAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates the Settings singleton from the supplied request.
    /// Throws <see cref="StageFright.Core.Exceptions.ValidationException"/> if the request is invalid or setup has already been completed.
    /// </summary>
    Task InitializeAsync(SetupRequest request, CancellationToken ct = default);
}
