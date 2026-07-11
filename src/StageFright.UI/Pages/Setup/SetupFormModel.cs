using System.ComponentModel.DataAnnotations;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;

namespace StageFright.UI.Pages.Setup;

internal sealed class SetupFormModel
{
    [Required(ErrorMessage = "Organisation name is required.")]
    [StringLength(255, ErrorMessage = "Organisation name must not exceed 255 characters.")]
    public string? OrganizationName { get; set; }

    [Required(ErrorMessage = "ABN is required.")]
#if !DEBUG
    // ABN checksum validation is skipped in Debug builds so developers can click through
    // setup with a placeholder ABN instead of needing a real, checksum-valid one.
    [Abn]
#endif
    public string? Abn { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Annual fee must be zero or greater.")]
    public decimal AnnualFee { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Attendance fee must be zero or greater.")]
    public decimal AttendanceFee { get; set; }

    [Range(1, 12, ErrorMessage = "Renewal month must be between 1 and 12.")]
    public int MembershipRenewalMonth { get; set; } = 1;

    public bool IsGstRegistered { get; set; }

    public GstCode? AnnualFeeGstCode { get; set; }

    public GstCode? AttendanceFeeGstCode { get; set; }
}
