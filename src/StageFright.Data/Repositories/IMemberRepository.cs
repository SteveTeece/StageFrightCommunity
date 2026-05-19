namespace StageFright.Data.Repositories;

using StageFright.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Repository interface for Member entity operations.
/// Supports member lifecycle management with status filtering and effective dating.
/// </summary>
public interface IMemberRepository : IRepository<Member>
{
	/// <summary>Gets all active members.</summary>
	Task<IEnumerable<Member>> GetActiveMembersAsync();

	/// <summary>Gets all inactive members.</summary>
	Task<IEnumerable<Member>> GetInactiveMembersAsync();

	/// <summary>Gets members active as of a specific date (effective dating).</summary>
	Task<IEnumerable<Member>> GetHistoricalActiveMembersAsync(DateTime asOfDate);

	/// <summary>Gets a member by email address.</summary>
	Task<Member?> GetByEmailAsync(string email);

	/// <summary>Gets active count.</summary>
	Task<int> GetActiveMemberCountAsync();
}
