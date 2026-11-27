using System.ComponentModel.DataAnnotations;
using StageFright.Core.Enums;
using StageFright.UI.Shared;

namespace StageFright.UI.Pages.Setup;

// Public (not internal) because it's now a [Parameter] on the Tabs/* components
// (StageFright.UI.Pages.Setup.Tabs), and Blazor component parameters must be public.
public sealed class SetupFormModel : IValidatableObject
{
    [Required(ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_OrgNameRequired))]
    [StringLength(255, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_OrgNameTooLong))]
    public string? OrganizationName { get; set; }

    [Range(0, double.MaxValue, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_AnnualFeeNotNegative))]
    public decimal AnnualFee { get; set; }

    [Range(0, double.MaxValue, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_AttendanceFeeNotNegative))]
    public decimal AttendanceFee { get; set; }

    [Range(1, 12, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_RenewalMonthRange))]
    public int MembershipRenewalMonth { get; set; } = 1;

    /// <summary>
    /// ISO 4217 code of the currency the organisation will keep its books in (spec 028, FR-001).
    /// Mandatory, always-visible picker; defaults to <c>"AUD"</c>. Fixed for the life of the
    /// dataset once setup completes (FR-002).
    /// </summary>
    [Required(ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_CurrencyRequired))]
    public string CurrencyCode { get; set; } = "AUD";

    /// <summary>
    /// Month (1–12) the financial year starts on (spec 028, US7 / FR-019). Mandatory,
    /// always-visible picker; defaults to July (Australian FY).
    /// </summary>
    [Range(1, 12, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_FinancialYearStartMonthRange))]
    public int FinancialYearStartMonth { get; set; } = 7;

    /// <summary>
    /// Day of <see cref="FinancialYearStartMonth"/> (1–28) the financial year starts on
    /// (spec 028, US7 / FR-020). Mandatory picker; defaults to the 1st. The 28 upper bound
    /// avoids month-length edge cases.
    /// </summary>
    [Range(1, 28, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_FinancialYearStartDayRange))]
    public int FinancialYearStartDay { get; set; } = 1;

    /// <summary>
    /// Optional date the organisation was founded (spec 028, FR-022 / issue #353). Left blank by
    /// most groups; when set and later than the financial-year start, the first financial year is
    /// reported as a shorter "part-year". Persisted onto <see cref="Core.Entities.Settings.InceptionDate"/>.
    /// </summary>
    public DateTime? InceptionDate { get; set; }

    public bool IsTaxApplicable { get; set; }

    public decimal? TaxRate { get; set; }

    public TaxCode? AnnualFeeTaxCode { get; set; }

    public TaxCode? AttendanceFeeTaxCode { get; set; }

    /// <summary>
    /// How a newly entered taxable amount is interpreted (spec 028, issue #354): tax-inclusive
    /// gross (the default) or tax-exclusive net with tax added on top. Only meaningful while
    /// <see cref="IsTaxApplicable"/> is true; persisted onto <see cref="Core.Entities.Settings.TaxEntryMode"/>.
    /// </summary>
    public TaxEntryMode TaxEntryMode { get; set; } = TaxEntryMode.Inclusive;

    [Range(1, 12, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Setup_Validation_AgmMonthRange))]
    public int CommitteeRenewalMonth { get; set; } = 1;

    public int? GeneralCommitteeSeatCountTarget { get; set; }

    [Range(1, 7, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Validation_Settings_AuditRetentionRange))]
    public int AuditRetentionYears { get; set; } = 5;

    /// <summary>
    /// TaxRate is only required (and must be positive) while IsTaxApplicable is true — a
    /// plain [Range] on a nullable field never fires for a blank value, so the "required
    /// while applicable" rule needs this cross-field check instead.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsTaxApplicable && TaxRate is not (> 0))
        {
            yield return new ValidationResult(
                FormValidationMessages.Setup_Validation_TaxRatePositive,
                [nameof(TaxRate)]);
        }
    }
}
