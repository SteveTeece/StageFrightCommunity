using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings;

/// <summary>Input data for the first-run setup wizard.</summary>
public record SetupRequest(
    string OrganizationName,
    string Abn,
    decimal AnnualFee,
    decimal AttendanceFee,
    int MembershipRenewalMonth,
    bool IsGstRegistered,
    GstCode? AnnualFeeGstCode,
    GstCode? AttendanceFeeGstCode,
    Theme Theme,
    int CommitteeRenewalMonth = 1,
    IReadOnlyList<string>? CommitteeOfficeHolderTitles = null,
    int? GeneralCommitteeSeatCountTarget = null);
