namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Committee Membership entity.</summary>
public class CommitteeMembershipRepository : BaseRepository<CommitteeMembership>, ICommitteeMembershipRepository
{
	public CommitteeMembershipRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<CommitteeMembership>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(cm => cm.MemberId == memberId && !cm.IsDeleted)
			.OrderByDescending(cm => cm.Year)
			.ToListAsync();
	}

	public async Task<IEnumerable<CommitteeMembership>> GetByYearAsync(int year)
	{
		return await _dbSet
			.Where(cm => cm.Year == year && !cm.IsDeleted)
			.ToListAsync();
	}

	public async Task RecordAsync(Guid memberId, int year, string position)
	{
		var membership = new CommitteeMembership
		{
			MemberId = memberId,
			Year = year,
			Position = position,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};
		await CreateAsync(membership);
	}

	public async Task ClearYearAsync(int year)
	{
		var memberships = await GetByYearAsync(year);
		foreach (var membership in memberships)
		{
			await SoftDeleteAsync(membership.Id);
		}
	}

	public async Task<IEnumerable<CommitteeMembership>> GetHistoryAsync(Guid memberId)
	{
		return await _dbSet
			.Where(cm => cm.MemberId == memberId)
			.OrderByDescending(cm => cm.Year)
			.ToListAsync();
	}
}
