namespace StageFright.Core.Modules.Settings;

/// <summary>Input data for the first-run setup wizard.</summary>
public record SetupRequest(
    string OrganizationName,
    decimal AnnualFee,
    decimal AttendanceFee,
    int MembershipRenewalMonth);
