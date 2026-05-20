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

	public async Task UpdateStoredParticipationRateAsync(Guid eventId, decimal participationRate)
	{
		var ev = await GetByIdAsync(eventId);
		if (ev == null)
			throw new InvalidOperationException($"Event with ID {eventId} not found.");

		ev.StoredParticipationRate = Math.Min(Math.Max(participationRate, 0), 100); // Clamp to 0-100
		await UpdateAsync(ev);
	}
}
