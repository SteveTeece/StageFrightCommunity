namespace StageFright.Data.Repositories;

using StageFright.Core.Entities;
using StageFright.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
