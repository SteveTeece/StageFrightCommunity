namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Attendance entity.</summary>
public class AttendanceRepository : BaseRepository<Attendance>, IAttendanceRepository
{
	public AttendanceRepository(StageFrightContext context) : base(context) { }

	public async Task RecordAsync(Guid rehearsalId, Guid memberId)
	{
		var attendance = new Attendance
		{
			RehearsalId = rehearsalId,
			MemberId = memberId,
			RecordedAt = DateTime.UtcNow
		};
		await CreateAsync(attendance);
	}

	public async Task<IEnumerable<Attendance>> GetByRehearsalAsync(Guid rehearsalId)
	{
		return await _dbSet
			.Where(a => a.RehearsalId == rehearsalId)
			.ToListAsync();
	}

	public async Task<IEnumerable<Attendance>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(a => a.MemberId == memberId)
			.ToListAsync();
	}

	public async Task<decimal> GetAttendanceRateAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		var rehearsals = await _context.Rehearsals
			.Where(r => r.Date >= fromDate && r.Date <= toDate && !r.IsDeleted)
			.ToListAsync();

		if (rehearsals.Count == 0)
			return 0;

		var attendances = await _dbSet
			.Where(a => a.MemberId == memberId && rehearsals.Select(r => r.Id).Contains(a.RehearsalId))
			.ToListAsync();

		return (decimal)attendances.Count / rehearsals.Count * 100;
	}
}
