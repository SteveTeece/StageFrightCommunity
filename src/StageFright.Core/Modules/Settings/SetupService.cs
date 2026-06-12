using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Handles first-run initialization. Validates the setup request, creates the Settings singleton,
/// and audits the event. System categories (Cash/MemberReceivable/BadDebtExpense) are seeded
/// by EF migrations and require no action here.
/// </summary>
public class SetupService : ISetupService
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IAuditTrailService _audit;

    public SetupService(ISettingsRepository settingsRepo, ICategoryRepository categoryRepo, IAuditTrailService audit)
    {
        _settingsRepo = settingsRepo;
        _categoryRepo = categoryRepo;
        _audit = audit;
    }

    public async Task<bool> IsSetupCompleteAsync(CancellationToken ct = default)
    {
        var settings = await _settingsRepo.GetAsync(ct);
        return settings is not null;
    }

    public async Task InitializeAsync(SetupRequest request, CancellationToken ct = default)
    {
        var existing = await _settingsRepo.GetAsync(ct);
        if (existing is not null)
            throw new ValidationException("Setup has already been completed.", "Settings", nameof(InitializeAsync));

        Validate(request);

        var settings = new SettingsEntity
        {
            Id = Guid.NewGuid(),
            OrganizationName = request.OrganizationName.Trim(),
            AnnualFee = request.AnnualFee,
            AttendanceFee = request.AttendanceFee,
            MembershipRenewalMonth = request.MembershipRenewalMonth,
            CommitteeRenewalMonth = 1,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = Theme.Light,
            SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _settingsRepo.SaveAsync(settings, ct);

        await _audit.LogAsync(
            entityType: "Settings",
            entityId: settings.Id,
            action: AuditAction.Create,
            newValue: request.OrganizationName,
            ct: ct);
    }

    private static void Validate(SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
            throw new ValidationException("OrganizationName is required.", "Settings", nameof(InitializeAsync));

        if (request.AnnualFee < 0)
            throw new ValidationException("AnnualFee must be zero or greater.", "Settings", nameof(InitializeAsync));

        if (request.AttendanceFee < 0)
            throw new ValidationException("AttendanceFee must be zero or greater.", "Settings", nameof(InitializeAsync));

        if (request.MembershipRenewalMonth < 1 || request.MembershipRenewalMonth > 12)
            throw new ValidationException("MembershipRenewalMonth must be between 1 and 12.", "Settings", nameof(InitializeAsync));
    }
}
