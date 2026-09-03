using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Rejects a financial posting dated on or before <c>Settings.ClosedThroughDate</c> so a
/// reported prior period cannot be altered by a back-dated entry (spec 028, FR-016 / FR-017).
/// Consulted at the GL choke point (<c>GLRepository</c>). Throws a plain-message
/// <see cref="ClosedPeriodException"/> like the other GL-layer guards; the finance posting
/// forms map it to the localized <c>Validation_ClosedPeriod_PostingRejected</c> text.
/// </summary>
/// <remarks>
/// No first-run-setup carve-out is needed: setup always completes before any period can be
/// closed, so <c>Settings</c> is absent (or <c>ClosedThroughDate</c> is null) while opening
/// balances are entered and this guard is a no-op then (FR-018). A future story needing a
/// genuine bypass should follow the ambient <c>AuditTrailSuppressionScope</c> pattern rather
/// than add a parameter here.
/// </remarks>
public class ClosedPeriodGuard : IClosedPeriodGuard
{
    private readonly ISettingsRepository _settingsRepo;

    public ClosedPeriodGuard(ISettingsRepository settingsRepo)
    {
        _settingsRepo = settingsRepo;
    }

    public async Task EnsureOpen(DateTime postingDate, CancellationToken ct = default)
    {
        var settings = await _settingsRepo.GetAsync(ct);
        if (settings?.ClosedThroughDate is not { } closedThrough)
            return;

        // A posting dated exactly on the closed-through date is inside the closed period.
        if (postingDate.Date <= closedThrough.Date)
            throw new ClosedPeriodException(
                $"Posting date {postingDate:yyyy-MM-dd} falls in a closed financial period (closed through {closedThrough:yyyy-MM-dd}); operation cancelled.",
                "Settings", nameof(EnsureOpen));
    }
}
