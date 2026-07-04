namespace StageFright.Core.Modules.Finance;

/// <summary>
/// GL-derived organisation-level finance snapshot for the dashboard Finance tile.
/// All amounts follow the income-statement convention: user (non-system) categories only,
/// income = credits − debits, expenses = debits − credits.
/// </summary>
public class FinanceSummary
{
    /// <summary>All-time net funds: total income − total expenses up to the as-of date.</summary>
    public decimal CurrentBalance { get; init; }

    /// <summary>Income recorded in the as-of month, up to the as-of date.</summary>
    public decimal MonthIncome { get; init; }

    /// <summary>Expenses recorded in the as-of month, up to the as-of date.</summary>
    public decimal MonthExpenses { get; init; }
}
