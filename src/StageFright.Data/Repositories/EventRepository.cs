namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Event entity.</summary>
public class EventRepository : BaseRepository<Event>, IEventRepository
{
	public EventRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<Event>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
	{
		return await _dbSet
			.Where(e => e.Date >= startDate && e.Date <= endDate && !e.IsDeleted)
			.OrderBy(e => e.Date)
			.ToListAsync();
	}

	public async Task<Event?> GetMostRecentAsync()
	{
		return await _dbSet
			.Where(e => !e.IsDeleted)
			.OrderByDescending(e => e.Date)
			.FirstOrDefaultAsync();
	}
}
