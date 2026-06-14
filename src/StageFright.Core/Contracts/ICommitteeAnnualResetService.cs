namespace StageFright.Core.Contracts;

/// <summary>
/// Manages the annual committee position reset workflow and AGM reminder banner.
/// Implementation is registered in Phase 15 (CommitteeAnnualResetService).
/// </summary>
public interface ICommitteeAnnualResetService
{
    /// <summary>
    /// Soft-deletes all current-year CommitteeMembership records, records
    /// LastCommitteeResetYear in Settings, and writes an audit entry.
    /// </summary>
    Task ResetAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a non-null banner message when the AGM has been recorded for the
    /// current year, the annual reset has not yet been performed, and the AGM
    /// occurred more than 7 days ago. Returns null if no action is needed.
    /// </summary>
    Task<string?> CheckAgmBannerAsync(CancellationToken ct = default);
}
