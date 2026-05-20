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

	public async Task UpdateStoredAttendanceRateAsync(Guid rehearsalId, decimal attendanceRate)
	{
		var rehearsal = await GetByIdAsync(rehearsalId);
		if (rehearsal == null)
			throw new InvalidOperationException($"Rehearsal with ID {rehearsalId} not found.");

		rehearsal.StoredAttendanceRate = Math.Min(Math.Max(attendanceRate, 0), 100); // Clamp to 0-100
		await UpdateAsync(rehearsal);
	}
}
