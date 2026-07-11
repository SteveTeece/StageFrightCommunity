namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Outstanding balance (Member Receivable, net of payments/corrections) as of the end of
/// one calendar month, used by the dashboard Outstanding Balances tile's calendar-year chart.
/// </summary>
public class MonthlyOutstandingBalance
{
    /// <summary>Calendar year of the month.</summary>
    public int Year { get; init; }

    /// <summary>Calendar month (1–12).</summary>
    public int Month { get; init; }

    /// <summary>Outstanding balance as of the end of this month.</summary>
    public decimal OutstandingBalance { get; init; }
}
