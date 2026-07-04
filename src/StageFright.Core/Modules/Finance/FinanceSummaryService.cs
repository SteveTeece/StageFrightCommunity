using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Computes organisation-level finance figures from the GL using the same category
/// conventions as the Income Statement report: only non-system Income/Expense categories
/// count, income = credits − debits, expenses = debits − credits. This avoids
/// double-counting the receivable legs of double-entry fee/payment transactions.
/// </summary>
public class FinanceSummaryService : IFinanceSummaryService
{
    private readonly IGLRepository _glRepository;
    private readonly ICategoryRepository _categoryRepository;

    public FinanceSummaryService(IGLRepository glRepository, ICategoryRepository categoryRepository)
    {
        _glRepository = glRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<FinanceSummary> GetSummaryAsync(DateTime asOf, CancellationToken ct = default)
    {
        var endOfDay = asOf.Date.AddDays(1).AddTicks(-1);
        var monthStart = new DateTime(asOf.Year, asOf.Month, 1, 0, 0, 0, asOf.Kind);

        var (incomeCategoryIds, expenseCategoryIds) = await GetUserCategoryIdsAsync(ct);
        var transactions = await _glRepository.GetByDateRangeAsync(DateTime.MinValue, endOfDay, ct);

        var (totalIncome, totalExpenses) = SumIncomeAndExpenses(transactions, incomeCategoryIds, expenseCategoryIds);

        var monthTransactions = transactions.Where(t => t.Date >= monthStart).ToList();
        var (monthIncome, monthExpenses) = SumIncomeAndExpenses(monthTransactions, incomeCategoryIds, expenseCategoryIds);

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

        var (incomeCategoryIds, expenseCategoryIds) = await GetUserCategoryIdsAsync(ct);
        var transactions = await _glRepository.GetByDateRangeAsync(firstMonth, endOfDay, ct);

        var byMonth = transactions
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<MonthlyCashFlow>(months);
        for (var i = 0; i < months; i++)
        {
            var month = firstMonth.AddMonths(i);
            var monthTransactions = byMonth.GetValueOrDefault((month.Year, month.Month), []);
            var (income, expenses) = SumIncomeAndExpenses(monthTransactions, incomeCategoryIds, expenseCategoryIds);

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

    private async Task<(HashSet<Guid> IncomeIds, HashSet<Guid> ExpenseIds)> GetUserCategoryIdsAsync(CancellationToken ct)
    {
        var categories = await _categoryRepository.GetAllAsync(ct);

        var incomeIds = categories
            .Where(c => c.Type == CategoryType.Income && !c.IsSystem)
            .Select(c => c.Id)
            .ToHashSet();

        var expenseIds = categories
            .Where(c => c.Type == CategoryType.Expense && !c.IsSystem)
            .Select(c => c.Id)
            .ToHashSet();

        return (incomeIds, expenseIds);
    }

    private static (decimal Income, decimal Expenses) SumIncomeAndExpenses(
        IReadOnlyCollection<Transaction> transactions,
        HashSet<Guid> incomeCategoryIds,
        HashSet<Guid> expenseCategoryIds)
    {
        var income = transactions
            .Where(t => incomeCategoryIds.Contains(t.CategoryId))
            .Sum(t => t.CreditAmount - t.DebitAmount);

        var expenses = transactions
            .Where(t => expenseCategoryIds.Contains(t.CategoryId))
            .Sum(t => t.DebitAmount - t.CreditAmount);

        return (income, expenses);
    }
}
