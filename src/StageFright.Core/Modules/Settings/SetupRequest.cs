using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings;

/// <summary>Input data for the first-run setup wizard.</summary>
public record SetupRequest(
    string OrganizationName,
    decimal AnnualFee,
    decimal AttendanceFee,
    int MembershipRenewalMonth,
    bool IsTaxApplicable,
    decimal? TaxRate,
    TaxCode? AnnualFeeTaxCode,
    TaxCode? AttendanceFeeTaxCode,
    Theme Theme,
    int CommitteeRenewalMonth = 1,
    IReadOnlyList<string>? CommitteeOfficeHolderTitles = null,
    int? GeneralCommitteeSeatCountTarget = null,
    int AuditRetentionYears = 1);
