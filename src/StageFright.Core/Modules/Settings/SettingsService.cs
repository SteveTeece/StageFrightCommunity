using System.Text.Json;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Thin application-layer wrapper over ISettingsRepository.
/// Reads and persists the Settings singleton; audits field-level changes on Save.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly IAuditTrailService _audit;

    public SettingsService(ISettingsRepository repository, IAuditTrailService audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<global::StageFright.Core.Entities.Settings?> GetAsync(CancellationToken ct = default) =>
        _repository.GetAsync(ct);

    public async Task SaveAsync(global::StageFright.Core.Entities.Settings settings, CancellationToken ct = default)
    {
        // Enforce Settings.IsGstRegistered's documented invariant here — the single choke
        // point every save path (General tab, GST tab, etc.) goes through — so un-registering
        // GST post-setup can't leave stale GST codes persisted (matches SetupService.InitializeAsync's
        // setup-time behavior).
        if (!settings.IsGstRegistered)
        {
            settings.AnnualFeeGstCode = null;
            settings.AttendanceFeeGstCode = null;
        }

#if !DEBUG
        // ABN checksum validation is skipped in Debug builds — see SetupFormModel.Abn.
        if (!string.IsNullOrEmpty(settings.Abn) && !AbnValidator.IsValid(settings.Abn))
            throw new ValidationException("The ABN is not valid.", "Settings", nameof(SaveAsync));
#endif

        if (settings.MinimumMemberAge < 0)
            throw new ValidationException("Minimum member age cannot be negative.", "Settings", nameof(SaveAsync));

        if (settings.MaxAgeRangeYears < 0)
            throw new ValidationException("Maximum age range cannot be negative.", "Settings", nameof(SaveAsync));

        if (settings.MinimumMemberAge > settings.MaxAgeRangeYears)
            throw new ValidationException("Minimum member age cannot exceed the maximum age range.", "Settings", nameof(SaveAsync));

        var existing = await _repository.GetAsync(ct);

        string? oldValue = existing is null
            ? null
            : JsonSerializer.Serialize(new { existing.OrganizationName, existing.AnnualFee, existing.AttendanceFee, existing.Theme });

        string newValue = JsonSerializer.Serialize(new { settings.OrganizationName, settings.AnnualFee, settings.AttendanceFee, settings.Theme });

        settings.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveAsync(settings, ct);

        await _audit.LogAsync(
            entityType: "Settings",
            entityId: settings.Id,
            action: AuditAction.Update,
            oldValue: oldValue,
            newValue: newValue,
            ct: ct);
    }
}
