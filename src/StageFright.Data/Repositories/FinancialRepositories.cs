namespace StageFright.Data.Repositories;

using StageFright.Core.Entities;
using StageFright.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository interface for Fee operations (immutable).</summary>
public interface IFeeRepository : IRepository<Fee>
{
	/// <summary>Gets all fees for a member.</summary>
	Task<IEnumerable<Fee>> GetByMemberAsync(Guid memberId);

	/// <summary>Gets unpaid fees for a member.</summary>
	Task<IEnumerable<Fee>> GetUnpaidAsync(Guid memberId);

	/// <summary>Gets fees for a specific year.</summary>
	Task<IEnumerable<Fee>> GetByYearAsync(int year);
}

/// <summary>Repository interface for Payment operations.</summary>
public interface IPaymentRepository : IRepository<Payment>
{
	/// <summary>Gets all payments for a member.</summary>
	Task<IEnumerable<Payment>> GetByMemberAsync(Guid memberId);

	/// <summary>Gets payment history for a member within date range.</summary>
	Task<IEnumerable<Payment>> GetPaymentHistoryAsync(Guid memberId, DateTime fromDate, DateTime toDate);

	/// <summary>Updates only the Notes field (other fields are immutable).</summary>
	Task UpdateNotesAsync(Guid paymentId, string notes);
}

/// <summary>Repository interface for Transaction operations (GL paired, immutable).</summary>
public interface ITransactionRepository : IRepository<Transaction>
{
	/// <summary>Gets transactions by category.</summary>
	Task<IEnumerable<Transaction>> GetByCategoryAsync(string category);

	/// <summary>Gets transactions for a member.</summary>
	Task<IEnumerable<Transaction>> GetByMemberAsync(Guid memberId);

	/// <summary>Gets transactions within date range.</summary>
	Task<IEnumerable<Transaction>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);

	/// <summary>Creates a paired GL transaction (debit and credit).</summary>
	Task CreatePairAsync(Transaction debit, Transaction credit);

	/// <summary>Validates GL balance (total debits = total credits).</summary>
	Task<bool> ValidateGLBalanceAsync();
}

/// <summary>Repository implementation for Fee entity (immutable).</summary>
public class FeeRepository : BaseRepository<Fee>, IFeeRepository
{
	public FeeRepository(StageFrightContext context) : base(context) { }

	public override async Task UpdateAsync(Fee entity)
	{
		throw new InvalidOperationException("Fees are immutable and cannot be updated.");
	}

	public async Task<IEnumerable<Fee>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(f => f.MemberId == memberId)
			.OrderByDescending(f => f.FeeDate)
			.ToListAsync();
	}

	public async Task<IEnumerable<Fee>> GetUnpaidAsync(Guid memberId)
	{
		return await _dbSet
			.Where(f => f.MemberId == memberId && f.DueDate <= DateTime.Now)
			.ToListAsync();
	}

	public async Task<IEnumerable<Fee>> GetByYearAsync(int year)
	{
		return await _dbSet
			.Where(f => f.FeeDate.Year == year)
			.ToListAsync();
	}
}

/// <summary>Repository implementation for Payment entity.</summary>
public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
	public PaymentRepository(StageFrightContext context) : base(context) { }

	public override async Task UpdateAsync(Payment entity)
	{
		// Only allow Notes to be updated
		var existing = await GetByIdAsync(entity.Id);
		if (existing == null)
			throw new InvalidOperationException($"Payment with ID {entity.Id} not found.");

		existing.Notes = entity.Notes;
		existing.UpdatedAt = DateTime.UtcNow;
		_dbSet.Update(existing);
		await SaveChangesAsync();
	}

	public async Task<IEnumerable<Payment>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(p => p.MemberId == memberId)
			.OrderByDescending(p => p.Date)
			.ToListAsync();
	}

	public async Task<IEnumerable<Payment>> GetPaymentHistoryAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		return await _dbSet
			.Where(p => p.MemberId == memberId && p.Date >= fromDate && p.Date <= toDate)
			.OrderByDescending(p => p.Date)
			.ToListAsync();
	}

	public async Task UpdateNotesAsync(Guid paymentId, string notes)
	{
		var payment = await GetByIdAsync(paymentId);
		if (payment == null)
			throw new InvalidOperationException($"Payment with ID {paymentId} not found.");

		payment.Notes = notes;
		payment.UpdatedAt = DateTime.UtcNow;
		await UpdateAsync(payment);
	}
}

/// <summary>Repository implementation for Transaction entity (GL paired, immutable).</summary>
public class TransactionRepository : BaseRepository<Transaction>, ITransactionRepository
{
	public TransactionRepository(StageFrightContext context) : base(context) { }

	public override async Task UpdateAsync(Transaction entity)
	{
		throw new InvalidOperationException("Transactions are immutable and cannot be updated.");
	}

	public async Task<IEnumerable<Transaction>> GetByCategoryAsync(string category)
	{
		return await _dbSet
			.Where(t => t.Category == category)
			.OrderByDescending(t => t.Date)
			.ToListAsync();
	}

	public async Task<IEnumerable<Transaction>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(t => t.MemberId == memberId)
			.OrderByDescending(t => t.Date)
			.ToListAsync();
	}

	public async Task<IEnumerable<Transaction>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate)
	{
		return await _dbSet
			.Where(t => t.Date >= fromDate && t.Date <= toDate)
			.OrderByDescending(t => t.Date)
			.ToListAsync();
	}

	public async Task CreatePairAsync(Transaction debit, Transaction credit)
	{
		debit.CreatedAt = DateTime.UtcNow;
		debit.ModifiedAt = DateTime.UtcNow;
		credit.CreatedAt = DateTime.UtcNow;
		credit.ModifiedAt = DateTime.UtcNow;

		_dbSet.Add(debit);
		_dbSet.Add(credit);
		await SaveChangesAsync();
	}

	public async Task<bool> ValidateGLBalanceAsync()
	{
		var totalDebits = await _dbSet
			.Where(t => t.DebitAmount.HasValue)
			.SumAsync(t => t.DebitAmount.Value);

		var totalCredits = await _dbSet
			.Where(t => t.CreditAmount.HasValue)
			.SumAsync(t => t.CreditAmount.Value);

		// Allow 0.01 precision for rounding errors
		return Math.Abs(totalDebits - totalCredits) <= 0.01m;
	}
}
