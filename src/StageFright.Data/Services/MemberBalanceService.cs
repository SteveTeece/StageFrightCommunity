namespace StageFright.Data.Services;

using StageFright.Core.Entities;
using StageFright.Data.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Service for calculating member account balances.
/// Provides methods to get unpaid fee totals and balance breakdowns.
/// </summary>
public class MemberBalanceService
{
	private readonly IFeeRepository _feeRepository;

	/// <summary>Initializes a new instance of the MemberBalanceService.</summary>
	/// <param name="feeRepository">The fee repository for balance queries.</param>
	public MemberBalanceService(IFeeRepository feeRepository)
	{
		_feeRepository = feeRepository ?? throw new ArgumentNullException(nameof(feeRepository));
	}

	/// <summary>
	/// Gets the total outstanding balance for a member (unpaid annual + attendance fees).
	/// </summary>
	/// <param name="memberId">The member's ID.</param>
	/// <returns>The total unpaid balance.</returns>
	public async Task<decimal> GetMemberBalanceAsync(Guid memberId)
	{
		var unpaidFees = await _feeRepository.GetUnpaidAsync(memberId);
		return unpaidFees.Sum(f => f.Amount);
	}

	/// <summary>
	/// Gets a detailed balance breakdown by fee type (Annual vs Attendance).
	/// </summary>
	/// <param name="memberId">The member's ID.</param>
	/// <returns>
	/// A tuple containing (annualFeeBalance, attendanceFeeBalance, totalBalance).
	/// </returns>
	public async Task<(decimal AnnualFees, decimal AttendanceFees, decimal Total)> GetMemberBalanceBreakdownAsync(Guid memberId)
	{
		var unpaidFees = await _feeRepository.GetUnpaidAsync(memberId);

		var annualFees = unpaidFees
			.Where(f => f.FeeType == "Annual")
			.Sum(f => f.Amount);

		var attendanceFees = unpaidFees
			.Where(f => f.FeeType == "Attendance")
			.Sum(f => f.Amount);

		var total = annualFees + attendanceFees;

		return (annualFees, attendanceFees, total);
	}

	/// <summary>
	/// Gets all unpaid fees for a member with details for display.
	/// </summary>
	/// <param name="memberId">The member's ID.</param>
	/// <returns>List of unpaid fees ordered by date.</returns>
	public async Task<System.Collections.Generic.IEnumerable<Fee>> GetMemberUnpaidFeesAsync(Guid memberId)
	{
		return await _feeRepository.GetUnpaidAsync(memberId);
	}

	/// <summary>
	/// Determines if a member has any outstanding balance.
	/// </summary>
	/// <param name="memberId">The member's ID.</param>
	/// <returns>True if member has unpaid fees, false otherwise.</returns>
	public async Task<bool> HasOutstandingBalanceAsync(Guid memberId)
	{
		var balance = await GetMemberBalanceAsync(memberId);
		return balance > 0;
	}

	/// <summary>
	/// Gets a formatted balance summary string for display.
	/// </summary>
	/// <param name="memberId">The member's ID.</param>
	/// <returns>A formatted string showing balance details.</returns>
	public async Task<string> GetFormattedBalanceSummaryAsync(Guid memberId)
	{
		var (annualFees, attendanceFees, total) = await GetMemberBalanceBreakdownAsync(memberId);

		if (total == 0)
			return "No outstanding balance";

		return $"Annual Fees: {annualFees:C} | Attendance Fees: {attendanceFees:C} | Total: {total:C}";
	}
}
