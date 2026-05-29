namespace StageFright.Reports.Providers;

using Microsoft.Extensions.Logging;
using StageFright.Data.Repositories;
using StageFright.Data.Services;
using StageFright.Plugins.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Income Statement report provider generating revenue and expense statements.
/// Implements IReportProvider for plugin discovery and invocation.
/// </summary>
public class IncomeStatementReportProvider : IReportProvider
{
	private readonly ITransactionRepository _transactionRepository;
	private readonly ICategoryRepository _categoryRepository;
	private readonly GlBalanceValidationService _glBalanceValidationService;
	private readonly ILogger<IncomeStatementReportProvider> _logger;

	public string ModuleName => "Finance";
	public string ReportId => "income-statement";
	public string ReportName => "Income Statement";
	public int DisplayOrder => 1;

	public IncomeStatementReportProvider(
		ITransactionRepository transactionRepository,
		ICategoryRepository categoryRepository,
		GlBalanceValidationService glBalanceValidationService,
		ILogger<IncomeStatementReportProvider> logger)
	{
		_transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
		_categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
		_glBalanceValidationService = glBalanceValidationService ?? throw new ArgumentNullException(nameof(glBalanceValidationService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ReportData> GenerateAsync(ReportFilter? filter = null)
	{
		try
		{
			// Validate GL balance before generating report
			var isBalanced = await _glBalanceValidationService.ValidateGLBalanceAsync();
			if (!isBalanced)
			{
				var balanceDetails = await _glBalanceValidationService.GetGLBalanceDetailsAsync();
				throw new InvalidOperationException(
					$"GL Balance Verification Failed: Total Debits ({balanceDetails.TotalDebits:C}) ≠ Total Credits ({balanceDetails.TotalCredits:C}). Please review and correct GL entries.");
			}

			var dateFrom = filter?.DateFrom ?? new DateTime(DateTime.Now.Year, 1, 1);
			var dateTo = filter?.DateTo ?? DateTime.Now;

			// Get all transactions within date range
			var transactions = await _transactionRepository.GetByDateRangeAsync(dateFrom, dateTo);
			var allCategories = await _categoryRepository.GetAllAsync();

			// Organize by income and expense
			var incomeTransactions = new List<(string Category, decimal Amount)>();
			var expenseTransactions = new List<(string Category, decimal Amount)>();

			foreach (var transaction in transactions)
			{
				var category = allCategories.FirstOrDefault(c => c.Name == transaction.Category);
				if (category == null) continue;

				decimal amount = transaction.CreditAmount ?? 0;
				if (category.Type == "Income")
				{
					incomeTransactions.Add((category.Name, amount));
				}
				else if (category.Type == "Expense")
				{
					amount = transaction.DebitAmount ?? 0;
					expenseTransactions.Add((category.Name, amount));
				}
			}

			// Aggregate by category
			var incomeByCategory = incomeTransactions
				.GroupBy(x => x.Category)
				.Select(g => (Category: g.Key, Amount: g.Sum(x => x.Amount)))
				.OrderBy(x => x.Category)
				.ToList();

			var expenseByCategory = expenseTransactions
				.GroupBy(x => x.Category)
				.Select(g => (Category: g.Key, Amount: g.Sum(x => x.Amount)))
				.OrderBy(x => x.Category)
				.ToList();

			// Build report rows
			var rows = new List<string[]>();
			decimal totalIncome = 0;
			decimal totalExpense = 0;

			// Income section
			rows.Add(new[] { "REVENUE", "" });
			foreach (var (category, amount) in incomeByCategory)
			{
				rows.Add(new[] { $"  {category}", amount.ToString("C", CultureInfo.CurrentCulture) });
				totalIncome += amount;
			}
			rows.Add(new[] { "Total Revenue", totalIncome.ToString("C", CultureInfo.CurrentCulture) });
			rows.Add(new[] { "", "" }); // Spacing

			// Expense section
			rows.Add(new[] { "EXPENSES", "" });
			foreach (var (category, amount) in expenseByCategory)
			{
				rows.Add(new[] { $"  {category}", amount.ToString("C", CultureInfo.CurrentCulture) });
				totalExpense += amount;
			}
			rows.Add(new[] { "Total Expenses", totalExpense.ToString("C", CultureInfo.CurrentCulture) });
			rows.Add(new[] { "", "" }); // Spacing

			// Net income
			decimal netIncome = totalIncome - totalExpense;
			rows.Add(new[] { "NET INCOME", netIncome.ToString("C", CultureInfo.CurrentCulture) });

			_logger.LogInformation("Income Statement generated successfully for period {DateFrom:yyyy-MM-dd} to {DateTo:yyyy-MM-dd}", dateFrom, dateTo);

			return new ReportData
			{
				ReportTitle = $"Income Statement - {dateFrom:MMMM d, yyyy} to {dateTo:MMMM d, yyyy}",
				ColumnHeaders = new[] { "Description", "Amount" },
				Rows = rows.ToArray(),
				Summaries = new Dictionary<string, string>
				{
					{ "Total Income", totalIncome.ToString("C", CultureInfo.CurrentCulture) },
					{ "Total Expense", totalExpense.ToString("C", CultureInfo.CurrentCulture) },
					{ "Net Income", netIncome.ToString("C", CultureInfo.CurrentCulture) }
				},
				GeneratedAt = DateTime.UtcNow
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating Income Statement report");
			throw;
		}
	}
}
