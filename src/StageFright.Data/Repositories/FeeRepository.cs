namespace StageFright.Data.Repositories;

using StageFright.Core.Entities;
using StageFright.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
