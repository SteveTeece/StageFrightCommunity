namespace StageFright.Data.Services;

using StageFright.Core.Entities;
using StageFright.Data.Repositories;
using StageFright.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Service for allocating payments to fees using FIFO (First-In-First-Out) algorithm.
/// Oldest unpaid fees are satisfied first.
/// </summary>
public class PaymentAllocationService
{
	private readonly IFeeRepository _feeRepository;
	private readonly IPaymentRepository _paymentRepository;

	/// <summary>Initializes a new instance of the PaymentAllocationService.</summary>
	/// <param name="feeRepository">The fee repository for querying unpaid fees.</param>
	/// <param name="paymentRepository">The payment repository for payment tracking.</param>
	public PaymentAllocationService(IFeeRepository feeRepository, IPaymentRepository paymentRepository)
	{
		_feeRepository = feeRepository ?? throw new ArgumentNullException(nameof(feeRepository));
		_paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
	}

	/// <summary>
	/// Allocates a payment amount to unpaid fees using FIFO algorithm.
	/// </summary>
	/// <param name="memberId">The member's ID.</param>
	/// <param name="paymentAmount">The payment amount to allocate.</param>
	/// <returns>
	/// A tuple containing:
	/// - List of (FeeId, AmountAllocated) tuples showing how payment was distributed
	/// - Remaining balance if payment exceeds total unpaid fees (member credit)
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when payment amount is invalid.</exception>
	public async Task<(List<(Guid FeeId, decimal AmountAllocated)> Allocations, decimal MemberCredit)> AllocatePaymentAsync(Guid memberId, decimal paymentAmount)
	{
		if (paymentAmount <= 0)
			throw new ArgumentException("Payment amount must be positive.", nameof(paymentAmount));

		var unpaidFees = (await _feeRepository.GetUnpaidAsync(memberId)).ToList();

		// Sort by FIFO: oldest fees first (by FeeDate, then by CreatedAt, then by Id as tiebreaker)
		var sortedFees = unpaidFees
			.OrderBy(f => f.FeeDate)
			.ThenBy(f => f.CreatedAt)
			.ThenBy(f => f.Id)
			.ToList();

		var allocations = new List<(Guid, decimal)>();
		var remainingAmount = paymentAmount;

		foreach (var fee in sortedFees)
		{
			if (remainingAmount <= 0)
				break;

			// Allocate the minimum of remaining payment or fee amount
			var allocationAmount = Math.Min(remainingAmount, fee.Amount);
			allocations.Add((fee.Id, allocationAmount));
			remainingAmount -= allocationAmount;
		}

		// Remaining amount becomes member credit (overpayment)
		var memberCredit = Math.Max(0, remainingAmount);

		return (allocations, memberCredit);
	}

	/// <summary>
	/// Gets a summary of FIFO allocation for a payment (for UI display).
	/// </summary>
	/// <param name="memberId">The member's ID.</param>
	/// <param name="paymentAmount">The payment amount.</param>
	/// <returns>
	/// A formatted summary showing fees being paid and any member credit created.
	/// </returns>
	public async Task<string> GetAllocationSummaryAsync(Guid memberId, decimal paymentAmount)
	{
		var (allocations, memberCredit) = await AllocatePaymentAsync(memberId, paymentAmount);

		if (allocations.Count == 0)
			return $"Payment of {paymentAmount:C} will be held as member credit.";

		var summary = new System.Text.StringBuilder();
		summary.AppendLine($"Payment allocation for ${paymentAmount:C}:");

		foreach (var (feeId, amount) in allocations)
		{
			summary.AppendLine($"  - Fee {feeId}: ${amount:C}");
		}

		if (memberCredit > 0)
			summary.AppendLine($"  - Member credit: ${memberCredit:C}");

		return summary.ToString();
	}
}
