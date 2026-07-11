using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Provides GL-derived organisation-level finance figures for the dashboard:
/// current balance, month-to-date income/expenses, and monthly cash-flow history.
/// </summary>
public interface IFinanceSummaryService
{
    /// <summary>
    /// Returns the organisation's current balance (all-time income − expenses) and the
    /// income/expense totals for the month containing <paramref name="asOf"/>, up to that date.
    /// </summary>
    Task<FinanceSummary> GetSummaryAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Returns one entry per calendar month for the <paramref name="months"/> months ending
    /// with the month of <paramref name="asOf"/>, oldest first. Months without transactions
    /// are included with zero totals.
    /// </summary>
    Task<IReadOnlyList<MonthlyCashFlow>> GetMonthlyCashFlowAsync(DateTime asOf, int months, CancellationToken ct = default);

    /// <summary>
    /// Returns outstanding member fee balances split by fee type (Attendance vs. Annual).
    /// </summary>
    Task<OutstandingFeeSummary> GetOutstandingFeeSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns one outstanding-balance snapshot per calendar month, from January through
    /// the month of <paramref name="asOf"/>, for the year of <paramref name="asOf"/>.
    /// </summary>
    Task<IReadOnlyList<MonthlyOutstandingBalance>> GetOutstandingBalanceTrendAsync(DateTime asOf, CancellationToken ct = default);
}
