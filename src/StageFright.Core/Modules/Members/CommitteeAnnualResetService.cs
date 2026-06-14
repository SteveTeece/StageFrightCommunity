using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Manages the annual committee position reset workflow and AGM reminder banner (FR-031).
/// </summary>
public class CommitteeAnnualResetService : ICommitteeAnnualResetService
{
    private readonly ICommitteeMembershipRepository _committeeMembershipRepo;
    private readonly ISettingsService _settingsService;
    private readonly IEventRepository _eventRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public CommitteeAnnualResetService(
        ICommitteeMembershipRepository committeeMembershipRepo,
        ISettingsService settingsService,
        IEventRepository eventRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _committeeMembershipRepo = committeeMembershipRepo;
        _settingsService = settingsService;
        _eventRepo = eventRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task ResetAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetAsync(ct)
            ?? throw new ValidationException(
                "Settings not initialised; cannot perform committee reset.",
                "Settings", "CommitteeReset");

        var currentYear = DateTime.UtcNow.Year;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _committeeMembershipRepo.SoftDeleteCurrentYearAsync(currentYear, "system", innerCt);

            settings.LastCommitteeResetYear = currentYear;
            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsService.SaveAsync(settings, innerCt);

            await _audit.LogAsync(
                "CommitteeMembership",
                Guid.Empty,
                AuditAction.CommitteeReset,
                newValue: currentYear.ToString(),
                ct: innerCt);
        }, ct);
    }

    /// <inheritdoc />
    public async Task<string?> CheckAgmBannerAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetAsync(ct);
        if (settings is null)
            return null;

        var currentYear = DateTime.UtcNow.Year;

        // Banner only needed when reset hasn't been done this year yet
        if (settings.LastCommitteeResetYear >= currentYear)
            return null;

        var agmExists = await _eventRepo.AgmExistsInYearAsync(currentYear, ct);
        if (!agmExists)
            return null;

        // Confirm the AGM occurred more than 7 days ago
        var mostRecent = await _eventRepo.GetMostRecentPastAsync(DateTime.UtcNow, ct);
        if (mostRecent is null)
            return null;

        var isAgmOldEnough = mostRecent.Date.Date <= DateTime.UtcNow.Date.AddDays(-7);
        if (!isAgmOldEnough)
            return null;

        return "Your Annual General Meeting has been recorded. It's time to reset committee positions for the new year. Click to reset.";
    }
}
