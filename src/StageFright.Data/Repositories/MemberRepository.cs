namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Repository implementation for Member entity.
/// Handles member CRUD operations and status filtering.
/// </summary>
public class MemberRepository : BaseRepository<Member>, IMemberRepository
{
	public MemberRepository(StageFrightContext context) : base(context)
	{
	}

	public async Task<IEnumerable<Member>> GetActiveMembersAsync()
	{
		return await _dbSet
			.Where(m => m.Status == "Active" && !m.IsDeleted)
			.ToListAsync();
	}

	public async Task<IEnumerable<Member>> GetInactiveMembersAsync()
	{
		return await _dbSet
			.Where(m => m.Status == "Inactive" && !m.IsDeleted)
			.ToListAsync();
	}

	public async Task<IEnumerable<Member>> GetHistoricalActiveMembersAsync(DateTime asOfDate)
	{
		return await _dbSet
			.Where(m => 
				m.JoinDate <= asOfDate && 
				(m.InactivateDate == null || m.InactivateDate > asOfDate) &&
				!m.IsDeleted)
			.ToListAsync();
	}

	public async Task<Member?> GetByEmailAsync(string email)
	{
		return await _dbSet
			.FirstOrDefaultAsync(m => m.Email == email && !m.IsDeleted);
	}

	public async Task<int> GetActiveMemberCountAsync()
	{
		return await _dbSet
			.Where(m => m.Status == "Active" && !m.IsDeleted)
			.CountAsync();
	}
}
