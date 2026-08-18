using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Events;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Handles first-run initialization. Validates the setup request, creates the Settings singleton,
/// seeds default event types, and audits the event.
/// System accounts (Cash/MemberReceivable/BadDebtExpense) are seeded by EF migrations.
/// </summary>
public class SetupService : ISetupService
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly IEventTypeRepository _eventTypeRepo;
    private readonly ICommitteeOfficeHolderTypeService _officeHolderTypeService;
    private readonly IAuditTrailService _audit;

    public SetupService(
        ISettingsRepository settingsRepo,
        IAccountRepository accountRepo,
        IEventTypeRepository eventTypeRepo,
        ICommitteeOfficeHolderTypeService officeHolderTypeService,
        IAuditTrailService audit)
    {
        _settingsRepo = settingsRepo;
        _accountRepo = accountRepo;
        _eventTypeRepo = eventTypeRepo;
        _officeHolderTypeService = officeHolderTypeService;
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

        // Tax codes and rate only ever apply while tax is applicable — force them null
        // otherwise, regardless of what the wizard happened to have selected before the
        // user toggled tax applicability off.
        var taxRate = request.IsTaxApplicable ? request.TaxRate : null;
        var annualFeeTaxCode = request.IsTaxApplicable ? request.AnnualFeeTaxCode : null;
        var attendanceFeeTaxCode = request.IsTaxApplicable ? request.AttendanceFeeTaxCode : null;

        var settings = new SettingsEntity
        {
            Id = Guid.NewGuid(),
            OrganizationName = request.OrganizationName.Trim(),
            AnnualFee = request.AnnualFee,
            AttendanceFee = request.AttendanceFee,
            MembershipRenewalMonth = request.MembershipRenewalMonth,
            IsTaxApplicable = request.IsTaxApplicable,
            TaxRate = taxRate,
            AnnualFeeTaxCode = annualFeeTaxCode,
            AttendanceFeeTaxCode = attendanceFeeTaxCode,
            CommitteeRenewalMonth = request.CommitteeRenewalMonth,
            GeneralCommitteeSeatCountTarget = request.GeneralCommitteeSeatCountTarget,
            AuditRetentionYears = request.AuditRetentionYears,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = request.Theme,
            SchemaVersion = "1.1.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _settingsRepo.SaveAsync(settings, ct);

        await SeedDefaultEventTypesAsync(ct);

        // Committee configuration is optional at every default (FR-021) — coordinators who
        // skip this step get no custom titles, identical to configuring none from Settings.
        if (request.CommitteeOfficeHolderTitles is { Count: > 0 })
        {
            foreach (var title in request.CommitteeOfficeHolderTitles)
                await _officeHolderTypeService.AddAsync(title, ct);
        }

        await _audit.LogAsync(
            entityType: "Settings",
            entityId: settings.Id,
            action: AuditAction.Create,
            newValue: request.OrganizationName,
            ct: ct);
    }

    private async Task SeedDefaultEventTypesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var name in EventTypeService.GetDefaultEventTypeNames())
        {
            var eventType = new EventType
            {
                Id = Guid.NewGuid(),
                Name = name,
                IsSystemDefault = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _eventTypeRepo.AddAsync(eventType, ct);
        }
    }

    private static void Validate(SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
            throw new ValidationException("OrganizationName is required.", "Settings", nameof(InitializeAsync));

        if (request.IsTaxApplicable && request.TaxRate is not (> 0))
            throw new ValidationException("A tax rate greater than zero is required when sales tax applies.", "Settings", nameof(InitializeAsync));

        if (request.AnnualFee < 0)
            throw new ValidationException("AnnualFee must be zero or greater.", "Settings", nameof(InitializeAsync));

        if (request.AttendanceFee < 0)
            throw new ValidationException("AttendanceFee must be zero or greater.", "Settings", nameof(InitializeAsync));

        if (request.MembershipRenewalMonth < 1 || request.MembershipRenewalMonth > 12)
            throw new ValidationException("MembershipRenewalMonth must be between 1 and 12.", "Settings", nameof(InitializeAsync));

        if (request.AuditRetentionYears < 1 || request.AuditRetentionYears > 7)
            throw new ValidationException("Audit retention period must be between 1 and 7 years.", "Settings", nameof(InitializeAsync));
    }
}
