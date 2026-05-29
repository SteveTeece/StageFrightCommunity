namespace StageFright.Reports.Providers;

using Microsoft.Extensions.Logging;
using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Account Register report provider showing chronological transaction list with running balance.
/// Supports date range and category filtering.
/// </summary>
public class AccountRegisterReportProvider : IReportProvider
{
	private readonly ITransactionRepository _transactionRepository;
	private readonly ILogger<AccountRegisterReportProvider> _logger;

	public string ModuleName => "Finance";
	public string ReportId => "account-register";
	public string ReportName => "Account Register";
	public int DisplayOrder => 3;

	public AccountRegisterReportProvider(
		ITransactionRepository transactionRepository,
		ILogger<AccountRegisterReportProvider> logger)
	{
		_transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ReportData> GenerateAsync(ReportFilter? filter = null)
	{
		try
		{
			var dateFrom = filter?.DateFrom ?? new DateTime(DateTime.Now.Year, 1, 1);
			var dateTo = filter?.DateTo ?? DateTime.Now;
			var categoryFilter = filter?.CategoryFilter;

			// Get all transactions within date range
			var transactions = await _transactionRepository.GetByDateRangeAsync(dateFrom, dateTo);

			// Apply category filter if specified
			if (!string.IsNullOrEmpty(categoryFilter))
			{
				transactions = transactions.Where(t => t.Category == categoryFilter).ToList();
			}

			// Sort chronologically
			var sortedTransactions = transactions.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToList();

			// Build report rows with running balance
			var rows = new List<string[]>();
			decimal runningBalance = 0m;

			foreach (var transaction in sortedTransactions)
			{
				decimal debit = transaction.DebitAmount ?? 0m;
				decimal credit = transaction.CreditAmount ?? 0m;

				// Calculate running balance (debits are positive, credits are negative)
				runningBalance += debit - credit;

				var description = string.IsNullOrEmpty(transaction.Description)
					? transaction.Category
					: transaction.Description;

				rows.Add(new[]
				{
					transaction.Date.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture),
					transaction.Category,
					description,
					debit > 0 ? debit.ToString("C", CultureInfo.CurrentCulture) : "",
					credit > 0 ? credit.ToString("C", CultureInfo.CurrentCulture) : "",
					runningBalance.ToString("C", CultureInfo.CurrentCulture)
				});
			}

			var totalDebits = sortedTransactions.Sum(t => t.DebitAmount ?? 0m);
			var totalCredits = sortedTransactions.Sum(t => t.CreditAmount ?? 0m);

			_logger.LogInformation("Account Register generated successfully with {TransactionCount} transactions", sortedTransactions.Count);

			return new ReportData
			{
				ReportTitle = $"Account Register - {dateFrom:MMMM d, yyyy} to {dateTo:MMMM d, yyyy}" +
					(string.IsNullOrEmpty(categoryFilter) ? "" : $" ({categoryFilter})"),
				ColumnHeaders = new[] { "Date", "GL Account", "Description", "Debit", "Credit", "Running Balance" },
				Rows = rows.ToArray(),
				Summaries = new Dictionary<string, string>
				{
					{ "Total Debits", totalDebits.ToString("C", CultureInfo.CurrentCulture) },
					{ "Total Credits", totalCredits.ToString("C", CultureInfo.CurrentCulture) },
					{ "Transaction Count", sortedTransactions.Count.ToString() },
					{ "Final Balance", runningBalance.ToString("C", CultureInfo.CurrentCulture) }
				},
				GeneratedAt = DateTime.UtcNow
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating Account Register report");
			throw;
		}
	}
}
