namespace StageFright.Core.Modules.Finance;

/// <summary>
/// GL-derived outstanding fee totals, split by FeeType, for the dashboard Outstanding
/// Balances tile. Scoped to the Member Receivable account, fee-linked transactions only.
/// </summary>
public class OutstandingFeeSummary
{
    /// <summary>Outstanding balance across all members' Attendance fees.</summary>
    public decimal OutstandingAttendanceFees { get; init; }

    /// <summary>Outstanding balance across all members' Annual fees.</summary>
    public decimal OutstandingAnnualFees { get; init; }
}
