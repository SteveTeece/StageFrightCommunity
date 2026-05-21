namespace StageFright.Core.Services;

using Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Service for member-related business logic.</summary>
public interface IMemberService
{
	Task<Member> CreateMemberAsync(Member member);
	Task<Member?> GetMemberByIdAsync(Guid id);
	Task<IEnumerable<Member>> GetActiveMembersAsync();
	Task<IEnumerable<Member>> GetInactiveMembersAsync();
	Task UpdateMemberAsync(Member member);
	Task InactivateMemberAsync(Guid id);
	Task ActivateMemberAsync(Guid id);
	Task<int> GetActiveMemberCountAsync();
	Task<int> CalculateAgeAsync(DateTime dateOfBirth);
}
