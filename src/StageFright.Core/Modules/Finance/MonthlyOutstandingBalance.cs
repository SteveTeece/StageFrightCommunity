namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Snapshot of the three Outstanding Balances tile metrics — member count and outstanding
/// balance by fee type — as of the end of one calendar month, used by the dashboard tile's
/// calendar-year trend chart.
/// </summary>
public class MonthlyOutstandingBalance
{
    /// <summary>Calendar year of the month.</summary>
    public int Year { get; init; }

    /// <summary>Calendar month (1–12).</summary>
    public int Month { get; init; }

    /// <summary>Number of members with a positive outstanding balance as of the end of this month.</summary>
    public int MemberCount { get; init; }

    /// <summary>Outstanding Attendance fee balance as of the end of this month.</summary>
    public decimal OutstandingAttendanceFees { get; init; }

    /// <summary>Outstanding Annual fee balance as of the end of this month.</summary>
    public decimal OutstandingAnnualFees { get; init; }
}
