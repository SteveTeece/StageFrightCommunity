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
/// Trial Balance report provider showing all GL accounts with debit/credit totals.
/// Verifies GL balance is within acceptable tolerance (0.01 precision).
/// </summary>
public class TrialBalanceReportProvider : IReportProvider
{
	private readonly ITransactionRepository _transactionRepository;
	private readonly GlBalanceValidationService _glBalanceValidationService;
	private readonly ILogger<TrialBalanceReportProvider> _logger;

	public string ModuleName => "Finance";
	public string ReportId => "trial-balance";
	public string ReportName => "Trial Balance";
	public int DisplayOrder => 2;

	public TrialBalanceReportProvider(
		ITransactionRepository transactionRepository,
		GlBalanceValidationService glBalanceValidationService,
		ILogger<TrialBalanceReportProvider> logger)
	{
		_transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
		_glBalanceValidationService = glBalanceValidationService ?? throw new ArgumentNullException(nameof(glBalanceValidationService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ReportData> GenerateAsync(ReportFilter? filter = null)
	{
		try
		{
			// Validate GL balance before generating report
			var balanceDetails = await _glBalanceValidationService.GetGLBalanceDetailsAsync();
			
			if (!balanceDetails.IsBalanced)
			{
				throw new InvalidOperationException(
					$"GL Balance Verification Failed: Total Debits ({balanceDetails.TotalDebits:C}) ≠ Total Credits ({balanceDetails.TotalCredits:C}). Please review and correct GL entries.");
			}

			var dateFrom = filter?.DateFrom ?? new DateTime(DateTime.Now.Year, 1, 1);
			var dateTo = filter?.DateTo ?? DateTime.Now;

			// Get all transactions within date range
			var transactions = await _transactionRepository.GetByDateRangeAsync(dateFrom, dateTo);

			// Organize by GL account (category)
			var accountBalances = new Dictionary<string, (decimal Debits, decimal Credits)>();

			foreach (var transaction in transactions)
			{
				if (!accountBalances.ContainsKey(transaction.Category))
				{
					accountBalances[transaction.Category] = (0m, 0m);
				}

				var (debits, credits) = accountBalances[transaction.Category];
				debits += transaction.DebitAmount ?? 0m;
				credits += transaction.CreditAmount ?? 0m;
				accountBalances[transaction.Category] = (debits, credits);
			}

			// Build report rows
			var rows = new List<string[]>();
			decimal totalDebits = 0m;
			decimal totalCredits = 0m;

			// Asset accounts (0100-0199)
			rows.Add(new[] { "ASSETS", "", "" });
			var assetAccounts = accountBalances
				.Where(a => a.Key.StartsWith("01"))
				.OrderBy(a => a.Key);
			foreach (var (account, (debits, credits)) in assetAccounts)
			{
				rows.Add(new[] { $"  {account}", debits.ToString("C", CultureInfo.CurrentCulture), credits.ToString("C", CultureInfo.CurrentCulture) });
				totalDebits += debits;
				totalCredits += credits;
			}

			rows.Add(new[] { "", "", "" }); // Spacing

			// Income accounts (1000-1099)
			rows.Add(new[] { "INCOME", "", "" });
			var incomeAccounts = accountBalances
				.Where(a => a.Key.StartsWith("10"))
				.OrderBy(a => a.Key);
			foreach (var (account, (debits, credits)) in incomeAccounts)
			{
				rows.Add(new[] { $"  {account}", debits.ToString("C", CultureInfo.CurrentCulture), credits.ToString("C", CultureInfo.CurrentCulture) });
				totalDebits += debits;
				totalCredits += credits;
			}

			rows.Add(new[] { "", "", "" }); // Spacing

			// Expense accounts (2000-2099)
			rows.Add(new[] { "EXPENSES", "", "" });
			var expenseAccounts = accountBalances
				.Where(a => a.Key.StartsWith("20"))
				.OrderBy(a => a.Key);
			foreach (var (account, (debits, credits)) in expenseAccounts)
			{
				rows.Add(new[] { $"  {account}", debits.ToString("C", CultureInfo.CurrentCulture), credits.ToString("C", CultureInfo.CurrentCulture) });
				totalDebits += debits;
				totalCredits += credits;
			}

			rows.Add(new[] { "", "", "" }); // Spacing

			// Bad debt/write-off accounts (9900)
			rows.Add(new[] { "WRITE-OFFS", "", "" });
			var writeOffAccounts = accountBalances
				.Where(a => a.Key.StartsWith("99"))
				.OrderBy(a => a.Key);
			foreach (var (account, (debits, credits)) in writeOffAccounts)
			{
				rows.Add(new[] { $"  {account}", debits.ToString("C", CultureInfo.CurrentCulture), credits.ToString("C", CultureInfo.CurrentCulture) });
				totalDebits += debits;
				totalCredits += credits;
			}

			rows.Add(new[] { "", "", "" }); // Spacing

			// Totals
			rows.Add(new[] { "TOTALS", totalDebits.ToString("C", CultureInfo.CurrentCulture), totalCredits.ToString("C", CultureInfo.CurrentCulture) });

			_logger.LogInformation("Trial Balance generated successfully with Total Debits: {Debits:C}, Total Credits: {Credits:C}", totalDebits, totalCredits);

			return new ReportData
			{
				ReportTitle = $"Trial Balance - {dateFrom:MMMM d, yyyy} to {dateTo:MMMM d, yyyy}",
				ColumnHeaders = new[] { "GL Account", "Debits", "Credits" },
				Rows = rows.ToArray(),
				Summaries = new Dictionary<string, string>
				{
					{ "Total Debits", totalDebits.ToString("C", CultureInfo.CurrentCulture) },
					{ "Total Credits", totalCredits.ToString("C", CultureInfo.CurrentCulture) },
					{ "Balance Status", balanceDetails.IsBalanced ? "BALANCED" : "OUT OF BALANCE" }
				},
				GeneratedAt = DateTime.UtcNow
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating Trial Balance report");
			throw;
		}
	}
}
