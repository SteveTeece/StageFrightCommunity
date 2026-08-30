using System.ComponentModel.DataAnnotations;
using StageFright.Core.Enums;

namespace StageFright.UI.Pages.Setup;

// Public (not internal) because it's now a [Parameter] on the Tabs/* components
// (StageFright.UI.Pages.Setup.Tabs), and Blazor component parameters must be public.
public sealed class SetupFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Organisation name is required.")]
    [StringLength(255, ErrorMessage = "Organisation name must not exceed 255 characters.")]
    public string? OrganizationName { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Annual fee must be zero or greater.")]
    public decimal AnnualFee { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Attendance fee must be zero or greater.")]
    public decimal AttendanceFee { get; set; }

    [Range(1, 12, ErrorMessage = "Renewal month must be between 1 and 12.")]
    public int MembershipRenewalMonth { get; set; } = 1;

    /// <summary>
    /// ISO 4217 code of the currency the organisation will keep its books in (spec 028, FR-001).
    /// Mandatory, always-visible picker; defaults to <c>"AUD"</c>. Fixed for the life of the
    /// dataset once setup completes (FR-002).
    /// </summary>
    [Required(ErrorMessage = "Currency is required.")]
    public string CurrencyCode { get; set; } = "AUD";

    /// <summary>
    /// Month (1–12) the financial year starts on (spec 028, US7 / FR-019). Mandatory,
    /// always-visible picker; defaults to July (Australian FY).
    /// </summary>
    [Range(1, 12, ErrorMessage = "Financial year start month must be between 1 and 12.")]
    public int FinancialYearStartMonth { get; set; } = 7;

    /// <summary>
    /// Day of <see cref="FinancialYearStartMonth"/> (1–28) the financial year starts on
    /// (spec 028, US7 / FR-020). Mandatory picker; defaults to the 1st. The 28 upper bound
    /// avoids month-length edge cases.
    /// </summary>
    [Range(1, 28, ErrorMessage = "Financial year start day must be between 1 and 28.")]
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

    [Range(1, 12, ErrorMessage = "AGM month must be between 1 and 12.")]
    public int CommitteeRenewalMonth { get; set; } = 1;

    public int? GeneralCommitteeSeatCountTarget { get; set; }

    [Range(1, 7, ErrorMessage = "Audit retention period must be between 1 and 7 years.")]
    public int AuditRetentionYears { get; set; } = 5;

    /// <summary>
    /// Chosen display language as a BCP-47 culture id (spec 027, US3 / FR-013). Pre-set by the
    /// language step to the FR-023-resolved default; persisted onto <c>Settings.LanguageCode</c>
    /// at Finish.
    /// </summary>
    public string? LanguageCode { get; set; }

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
                "Tax rate must be greater than zero.",
                [nameof(TaxRate)]);
        }
    }
}
