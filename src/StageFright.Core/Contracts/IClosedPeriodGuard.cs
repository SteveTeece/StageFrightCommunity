namespace StageFright.Core.Contracts;

/// <summary>
/// Guards the GL choke point against back-dated postings into a reported prior period
/// (spec 028, FR-016 / FR-017). Consulted by <c>GLRepository</c> before any transaction is
/// written.
/// </summary>
public interface IClosedPeriodGuard
{
    /// <summary>
    /// Returns normally when <paramref name="postingDate"/> falls in an open period; throws
    /// <see cref="Exceptions.ClosedPeriodException"/> when it is on or before
    /// <c>Settings.ClosedThroughDate</c>. A no-op before first-run setup or while no period is
    /// closed.
    /// </summary>
    Task EnsureOpen(DateTime postingDate, CancellationToken ct = default);
}
