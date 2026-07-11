namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Income and expense totals for one calendar month, used by the dashboard cash-flow tile.
/// Amounts follow the income-statement convention (non-system categories only).
/// </summary>
public class MonthlyCashFlow
{
    /// <summary>Calendar year of the month.</summary>
    public int Year { get; init; }

    /// <summary>Calendar month (1–12).</summary>
    public int Month { get; init; }

    /// <summary>Total income for the month.</summary>
    public decimal Income { get; init; }

    /// <summary>Total expenses for the month.</summary>
    public decimal Expenses { get; init; }
}
