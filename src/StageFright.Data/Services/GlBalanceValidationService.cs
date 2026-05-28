namespace StageFright.Data.Services;

using StageFright.Data.Repositories;
using System;
using System.Threading.Tasks;

/// <summary>
/// Service for validating GL (General Ledger) balance.
/// Verifies that total debits equal total credits with allowed precision.
/// </summary>
public class GlBalanceValidationService
{
	private readonly ITransactionRepository _transactionRepository;
	private const decimal PRECISION = 0.01m; // Allow $0.01 rounding error

	/// <summary>Initializes a new instance of the GlBalanceValidationService.</summary>
	/// <param name="transactionRepository">The transaction repository for GL queries.</param>
	public GlBalanceValidationService(ITransactionRepository transactionRepository)
	{
		_transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
	}

	/// <summary>
	/// Validates the GL balance (total debits = total credits).
	/// </summary>
	/// <returns>A task that returns true if balanced, false otherwise.</returns>
	public async Task<bool> ValidateGLBalanceAsync()
	{
		return await _transactionRepository.ValidateGLBalanceAsync();
	}

	/// <summary>
	/// Gets the GL balance with detailed debit and credit amounts.
	/// Useful for debugging and error reporting.
	/// </summary>
	/// <returns>A tuple containing (totalDebits, totalCredits, isBalanced, difference).</returns>
	public async Task<(decimal TotalDebits, decimal TotalCredits, bool IsBalanced, decimal Difference)> GetGLBalanceDetailsAsync()
	{
		// Get all transactions from repository (this is a helper for reporting GL status)
		var allTransactions = await _transactionRepository.GetByDateRangeAsync(
			DateTime.MinValue,
			DateTime.MaxValue);

		decimal totalDebits = 0m;
		decimal totalCredits = 0m;

		foreach (var transaction in allTransactions)
		{
			if (transaction.DebitAmount.HasValue)
				totalDebits += transaction.DebitAmount.Value;
			if (transaction.CreditAmount.HasValue)
				totalCredits += transaction.CreditAmount.Value;
		}

		var difference = Math.Abs(totalDebits - totalCredits);
		var isBalanced = difference <= PRECISION;

		return (totalDebits, totalCredits, isBalanced, difference);
	}

	/// <summary>
	/// Gets a user-friendly error message when GL is out of balance.
	/// </summary>
	/// <returns>A formatted error message with debit and credit totals.</returns>
	public async Task<string> GetGLBalanceErrorMessageAsync()
	{
		var (debits, credits, isBalanced, difference) = await GetGLBalanceDetailsAsync();

		if (isBalanced)
			return "GL is balanced.";

		return $"GL Balance Verification Failed: Total Debits ({debits:C}) ≠ Total Credits ({credits:C}). Difference: {difference:C}.";
	}
}
