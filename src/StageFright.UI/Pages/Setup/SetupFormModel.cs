using System.ComponentModel.DataAnnotations;
using StageFright.Core.Enums;

namespace StageFright.UI.Pages.Setup;

internal sealed class SetupFormModel : IValidatableObject
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

    public bool IsTaxApplicable { get; set; }

    public decimal? TaxRate { get; set; }

    public TaxCode? AnnualFeeTaxCode { get; set; }

    public TaxCode? AttendanceFeeTaxCode { get; set; }

    [Range(1, 12, ErrorMessage = "AGM month must be between 1 and 12.")]
    public int CommitteeRenewalMonth { get; set; } = 1;

    /// <summary>Optional, comma-separated custom office-holder titles entered during setup.</summary>
    public string? CommitteeOfficeHolderTitlesText { get; set; }

    public int? GeneralCommitteeSeatCountTarget { get; set; }

    [Range(1, 7, ErrorMessage = "Audit retention period must be between 1 and 7 years.")]
    public int AuditRetentionYears { get; set; } = 1;

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
