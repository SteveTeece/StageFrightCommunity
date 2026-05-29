namespace StageFright.Data.Services;

using StageFright.Core.Entities;
using StageFright.Data.Repositories;
using StageFright.Core.Exceptions;
using System;
using System.Threading.Tasks;

/// <summary>
/// Service for managing GL (General Ledger) paired transactions.
/// Ensures double-entry accounting principle: every transaction has a debit and credit.
/// </summary>
public class GlTransactionService
{
	private readonly ITransactionRepository _transactionRepository;
	private readonly ICategoryRepository _categoryRepository;

	/// <summary>Initializes a new instance of the GlTransactionService.</summary>
	/// <param name="transactionRepository">The transaction repository for persisting GL entries.</param>
	/// <param name="categoryRepository">The category repository for GL account lookups.</param>
	public GlTransactionService(ITransactionRepository transactionRepository, ICategoryRepository categoryRepository)
	{
		_transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
		_categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
	}

	/// <summary>
	/// Creates a paired GL transaction with debit and credit entries.
	/// Ensures the pair balances (debit amount = credit amount).
	/// </summary>
	/// <param name="amount">The transaction amount (must be positive).</param>
	/// <param name="debitCategory">The GL account/category for the debit entry.</param>
	/// <param name="creditCategory">The GL account/category for the credit entry.</param>
	/// <param name="description">Optional transaction description.</param>
	/// <param name="memberId">Optional member ID associated with the transaction.</param>
	/// <param name="paymentId">Optional payment ID for traceability.</param>
	/// <param name="transactionDate">Optional transaction date (defaults to today).</param>
	/// <returns>A task representing the async operation.</returns>
	/// <exception cref="ArgumentException">Thrown when amount is zero or negative, or categories are invalid.</exception>
	/// <exception cref="DataAccessException">Thrown when the transaction cannot be saved.</exception>
	public async Task CreatePairedTransactionAsync(
		decimal amount,
		string debitCategory,
		string creditCategory,
		string? description = null,
		Guid? memberId = null,
		Guid? paymentId = null,
		DateTime? transactionDate = null)
	{
		if (amount <= 0)
			throw new ArgumentException("Transaction amount must be positive.", nameof(amount));

		if (string.IsNullOrWhiteSpace(debitCategory))
			throw new ArgumentException("Debit category cannot be null or empty.", nameof(debitCategory));

		if (string.IsNullOrWhiteSpace(creditCategory))
			throw new ArgumentException("Credit category cannot be null or empty.", nameof(creditCategory));

		if (debitCategory == creditCategory)
			throw new ArgumentException("Debit and credit categories must be different.", nameof(creditCategory));

		var date = transactionDate ?? DateTime.UtcNow.Date;

		try
		{
			// Create debit transaction (increases assets/expenses, decreases liabilities/income)
			var debitTransaction = new Transaction
			{
				Id = Guid.NewGuid(),
				Date = date,
				Category = debitCategory,
				DebitAmount = amount,
				CreditAmount = null,
				MemberId = memberId,
				PaymentId = paymentId,
				Description = description,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow
			};

			// Create credit transaction (increases liabilities/income, decreases assets/expenses)
			var creditTransaction = new Transaction
			{
				Id = Guid.NewGuid(),
				Date = date,
				Category = creditCategory,
				DebitAmount = null,
				CreditAmount = amount,
				MemberId = memberId,
				PaymentId = paymentId,
				Description = description,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow
			};

			// Create paired transactions atomically
			await _transactionRepository.CreatePairAsync(debitTransaction, creditTransaction);
		}
		catch (Exception ex) when (!(ex is ArgumentException))
		{
			throw new DataAccessException(
				$"Failed to create paired GL transaction for amount {amount:C}.",
				ex);
		}
	}

	/// <summary>
	/// Validates that the GL is balanced (total debits = total credits).
	/// </summary>
	/// <returns>A task that returns true if balanced, false otherwise.</returns>
	public async Task<bool> ValidateGLBalanceAsync()
	{
		return await _transactionRepository.ValidateGLBalanceAsync();
	}

	/// <summary>
	/// Creates GL transaction pair for a payment received.
	/// Debits Cash/Bank account (asset) and credits the payment category.
	/// </summary>
	/// <param name="payment">The payment record to create GL transactions for.</param>
	/// <param name="paymentCategory">The GL category code for the payment.</param>
	/// <returns>A task representing the async operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when payment is null.</exception>
	/// <exception cref="DataAccessException">Thrown when the transaction cannot be saved.</exception>
	public async Task CreatePaymentTransactionAsync(Payment payment, string paymentCategory)
	{
		if (payment == null)
			throw new ArgumentNullException(nameof(payment));

		if (string.IsNullOrWhiteSpace(paymentCategory))
			throw new ArgumentException("Payment category cannot be null or empty.", nameof(paymentCategory));

		try
		{
			// Create paired GL transaction:
			// Debit: Cash/Bank (Asset GL 0100) - increases cash received
			// Credit: Payment category (typically Income GL 10xx or reduction of payables)
			await CreatePairedTransactionAsync(
				amount: payment.Amount,
				debitCategory: "GL0100", // Cash/Bank account
				creditCategory: paymentCategory,
				description: $"Payment received: {payment.PaymentMethod}",
				memberId: payment.MemberId,
				paymentId: payment.Id,
				transactionDate: payment.Date
			);
		}
		catch (Exception ex) when (!(ex is ArgumentException))
		{
			throw new DataAccessException(
				$"Failed to create GL transaction for payment {payment.Id}.",
				ex);
		}
	}
}
