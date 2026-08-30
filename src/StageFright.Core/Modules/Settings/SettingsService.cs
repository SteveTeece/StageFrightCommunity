using System.Text.Json;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Thin application-layer wrapper over ISettingsRepository.
/// Reads and persists the Settings singleton; audits field-level changes on Save.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly IAuditTrailService _audit;
    private readonly ILocalizer _localizer;

    public SettingsService(ISettingsRepository repository, IAuditTrailService audit, ILocalizer localizer)
    {
        _repository = repository;
        _audit = audit;
        _localizer = localizer;
    }

    public Task<global::StageFright.Core.Entities.Settings?> GetAsync(CancellationToken ct = default) =>
        _repository.GetAsync(ct);

    public async Task SaveAsync(global::StageFright.Core.Entities.Settings settings, CancellationToken ct = default)
    {
        // Enforce Settings.IsTaxApplicable's documented invariant here — the single choke
        // point every save path (General tab, Sales Tax tab, etc.) goes through — so turning
        // tax off post-setup can't leave a stale rate/tax codes persisted (matches
        // SetupService.InitializeAsync's setup-time behavior).
        if (!settings.IsTaxApplicable)
        {
            settings.TaxRate = null;
            settings.AnnualFeeTaxCode = null;
            settings.AttendanceFeeTaxCode = null;
            settings.TaxEntryMode = TaxEntryMode.Inclusive;
        }
        else if (settings.TaxRate is not (> 0))
        {
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_TaxRateRequired"), "Settings", nameof(SaveAsync));
        }

        if (settings.MinimumMemberAge < 0)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_MinimumAgeNegative"), "Settings", nameof(SaveAsync));

        if (settings.MaxAgeRangeYears < 0)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_MaxAgeRangeNegative"), "Settings", nameof(SaveAsync));

        if (settings.MinimumMemberAge > settings.MaxAgeRangeYears)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_MinAgeExceedsMax"), "Settings", nameof(SaveAsync));

        if (settings.AuditRetentionYears < 1 || settings.AuditRetentionYears > 7)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_AuditRetentionRange"), "Settings", nameof(SaveAsync));

        var existing = await _repository.GetAsync(ct);

        // Currency is chosen once at first-run setup and fixed for the life of the dataset
        // (spec 028, FR-002) — no Settings edit surface renders a currency control, so a
        // differing incoming value can only be a bug or a tampered request.
        if (existing is not null
            && !string.IsNullOrWhiteSpace(existing.CurrencyCode)
            && !string.Equals(existing.CurrencyCode, settings.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_CurrencyImmutable"), "Settings", nameof(SaveAsync));
        }

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
