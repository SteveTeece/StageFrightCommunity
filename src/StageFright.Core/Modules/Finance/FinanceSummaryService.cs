using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Computes organisation-level finance figures from the GL using the same account
/// conventions as the Income Statement report: only non-system Income/Expense accounts
/// count, income = credits − debits, expenses = debits − credits. This avoids
/// double-counting the receivable legs of double-entry fee/payment transactions.
/// </summary>
public class FinanceSummaryService : IFinanceSummaryService
{
    private readonly IGLRepository _glRepository;
    private readonly IAccountRepository _accountRepository;

    public FinanceSummaryService(IGLRepository glRepository, IAccountRepository accountRepository)
    {
        _glRepository = glRepository;
        _accountRepository = accountRepository;
    }

    public async Task<FinanceSummary> GetSummaryAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1).AddTicks(-1);
        var monthStart = new DateTime(asOf.Year, asOf.Month, 1, 0, 0, 0, asOf.Kind);

        var (incomeAccountIds, expenseAccountIds) = await GetUserAccountIdsAsync(ct);
        var transactions = await _glRepository.GetByDateRangeAsync(DateTime.MinValue, endOfDay, ct);

        var (totalIncome, totalExpenses) = SumIncomeAndExpenses(transactions, incomeAccountIds, expenseAccountIds);

        var monthTransactions = transactions.Where(t => t.Date >= monthStart).ToList();
        var (monthIncome, monthExpenses) = SumIncomeAndExpenses(monthTransactions, incomeAccountIds, expenseAccountIds);

        return new FinanceSummary
        {
            CurrentBalance = totalIncome - totalExpenses,
            MonthIncome = monthIncome,
            MonthExpenses = monthExpenses
        };
    }

    public async Task<IReadOnlyList<MonthlyCashFlow>> GetMonthlyCashFlowAsync(DateTime asOf, int months, CancellationToken ct = default)
    {
        if (months < 1)
            throw new ArgumentOutOfRangeException(nameof(months), months, "At least one month is required.");

        var endOfDay = asOf.Date.AddDays(1).AddTicks(-1);
        var firstMonth = new DateTime(asOf.Year, asOf.Month, 1, 0, 0, 0, asOf.Kind).AddMonths(-(months - 1));

        var (incomeAccountIds, expenseAccountIds) = await GetUserAccountIdsAsync(ct);
        var transactions = await _glRepository.GetByDateRangeAsync(firstMonth, endOfDay, ct);

        var byMonth = transactions
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<MonthlyCashFlow>(months);
        for (var i = 0; i < months; i++)
        {
            var month = firstMonth.AddMonths(i);
            var monthTransactions = byMonth.GetValueOrDefault((month.Year, month.Month), []);
            var (income, expenses) = SumIncomeAndExpenses(monthTransactions, incomeAccountIds, expenseAccountIds);

            result.Add(new MonthlyCashFlow
            {
                Year = month.Year,
                Month = month.Month,
                Income = income,
                Expenses = expenses
            });
        }

        return result;
    }

    public async Task<OutstandingFeeSummary> GetOutstandingFeeSummaryAsync(CancellationToken ct = default)
    {
        // No cutoff — the current snapshot includes every fee-linked transaction to date.
        var (attendance, annual) = await _glRepository.GetOutstandingByFeeTypeAsync(DateTime.MaxValue, ct);

        return new OutstandingFeeSummary
        {
            OutstandingAttendanceFees = attendance,
            OutstandingAnnualFees = annual
        };
    }

    public async Task<IReadOnlyList<MonthlyOutstandingBalance>> GetOutstandingBalanceTrendAsync(DateTime asOf, CancellationToken ct = default)
    {
        var result = new List<MonthlyOutstandingBalance>(asOf.Month);

        for (var month = 1; month <= asOf.Month; month++)
        {
            var endOfMonth = new DateTime(asOf.Year, month, 1, 0, 0, 0, asOf.Kind).AddMonths(1).AddTicks(-1);

            var memberCount = await _glRepository.GetOutstandingMemberCountAsync(endOfMonth, ct);
            var (attendance, annual) = await _glRepository.GetOutstandingByFeeTypeAsync(endOfMonth, ct);

            result.Add(new MonthlyOutstandingBalance
            {
                Year = asOf.Year,
                Month = month,
                MemberCount = memberCount,
                OutstandingAttendanceFees = attendance,
                OutstandingAnnualFees = annual
            });
        }

        return result;
    }

    private async Task<(HashSet<Guid> IncomeIds, HashSet<Guid> ExpenseIds)> GetUserAccountIdsAsync(CancellationToken ct)
    {
        var accounts = await _accountRepository.GetAllAsync(ct);

        var incomeIds = accounts
            .Where(c => c.Type == AccountType.Income && !c.IsSystem)
            .Select(c => c.Id)
            .ToHashSet();

        var expenseIds = accounts
            .Where(c => c.Type == AccountType.Expense && !c.IsSystem)
            .Select(c => c.Id)
            .ToHashSet();

        return (incomeIds, expenseIds);
    }

    private static (decimal Income, decimal Expenses) SumIncomeAndExpenses(
        IReadOnlyCollection<Transaction> transactions,
        HashSet<Guid> incomeAccountIds,
        HashSet<Guid> expenseAccountIds)
    {
        var income = transactions
            .Where(t => incomeAccountIds.Contains(t.AccountId))
            .Sum(t => t.CreditAmount - t.DebitAmount);

        var expenses = transactions
            .Where(t => expenseAccountIds.Contains(t.AccountId))
            .Sum(t => t.DebitAmount - t.CreditAmount);

        return (income, expenses);
    }
}
