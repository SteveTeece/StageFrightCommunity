namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Participation entity.</summary>
public class ParticipationRepository : BaseRepository<Participation>, IParticipationRepository
{
	public ParticipationRepository(StageFrightContext context) : base(context) { }

	public async Task RecordAsync(Guid eventId, Guid memberId)
	{
		var participation = new Participation
		{
			EventId = eventId,
			MemberId = memberId,
			RecordedAt = DateTime.UtcNow
		};
		await CreateAsync(participation);
	}

	public async Task<IEnumerable<Participation>> GetByEventAsync(Guid eventId)
	{
		return await _dbSet
			.Where(p => p.EventId == eventId)
			.ToListAsync();
	}

	public async Task<IEnumerable<Participation>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(p => p.MemberId == memberId)
			.ToListAsync();
	}

	public async Task<decimal> GetParticipationRateAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		var events = await _context.Events
			.Where(e => e.Date >= fromDate && e.Date <= toDate && !e.IsDeleted)
			.ToListAsync();

		if (events.Count == 0)
			return 0;

		var participations = await _dbSet
			.Where(p => p.MemberId == memberId && events.Select(e => e.Id).Contains(p.EventId))
			.ToListAsync();

		return (decimal)participations.Count / events.Count * 100;
	}
}
