namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Rehearsal entity.</summary>
public class RehearsalRepository : BaseRepository<Rehearsal>, IRehearsalRepository
{
	public RehearsalRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<Rehearsal>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
	{
		return await _dbSet
			.Where(r => r.Date >= startDate && r.Date <= endDate && !r.IsDeleted)
			.OrderBy(r => r.Date)
			.ToListAsync();
	}

	public async Task<Rehearsal?> GetMostRecentAsync()
	{
		return await _dbSet
			.Where(r => !r.IsDeleted)
			.OrderByDescending(r => r.Date)
			.FirstOrDefaultAsync();
	}
}
