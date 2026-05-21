namespace StageFright.Maui.Services;

using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Services;
using StageFright.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Service for committee membership tracking by year.</summary>
public class CommitteeMembershipService : ICommitteeMembershipService
{
	private readonly ICommitteeMembershipRepository _committeeMembershipRepository;
	private readonly IMemberRepository _memberRepository;

	public CommitteeMembershipService(
		ICommitteeMembershipRepository committeeMembershipRepository,
		IMemberRepository memberRepository)
	{
		_committeeMembershipRepository = committeeMembershipRepository;
		_memberRepository = memberRepository;
	}

	public async Task<IEnumerable<CommitteeMembership>> GetMemberCommitteeHistoryAsync(Guid memberId)
	{
		var member = await _memberRepository.GetByIdAsync(memberId);
		if (member == null)
			throw new EntityNotFoundException($"Member with ID {memberId} not found.");

		return await _committeeMembershipRepository.GetByMemberAsync(memberId);
	}

	public async Task<IEnumerable<CommitteeMembership>> GetCommitteeForYearAsync(int year)
	{
		return await _committeeMembershipRepository.GetByYearAsync(year);
	}

	public async Task RecordCommitteeMembershipAsync(Guid memberId, int year, string position)
	{
		var member = await _memberRepository.GetByIdAsync(memberId);
		if (member == null)
			throw new EntityNotFoundException($"Member with ID {memberId} not found.");

		if (string.IsNullOrWhiteSpace(position))
			throw new ValidationException("Position is required.");

		if (year < 2000 || year > 2100)
			throw new ValidationException("Year must be between 2000 and 2100.");

		// Check if member already has a position for this year
		var existing = await _committeeMembershipRepository.GetByMemberAndYearAsync(memberId, year);
		if (existing != null)
			throw new ValidationException($"Member already has a position ({existing.Position}) for year {year}.");

		var membership = new CommitteeMembership
		{
			MemberId = memberId,
			Year = year,
			Position = position,
			IsDeleted = false
		};

		await _committeeMembershipRepository.CreateAsync(membership);
	}

	public async Task ResetCommitteeYearAsync(int year)
	{
		var memberships = await _committeeMembershipRepository.GetByYearAsync(year);

		foreach (var membership in memberships)
		{
			membership.IsDeleted = true;
			await _committeeMembershipRepository.UpdateAsync(membership);
		}
	}

	public async Task<CommitteeMembership?> GetCurrentCommitteeMembershipAsync(Guid memberId)
	{
		int targetYear = DateTime.Today.Year;
		return await _committeeMembershipRepository.GetByMemberAndYearAsync(memberId, targetYear);
	}

	/// <summary>Updates member's committee position for current year.</summary>
	public async Task UpdateCommitteeMembershipAsync(Guid memberId, int year, string newPosition)
	{
		var membership = await _committeeMembershipRepository.GetByMemberAndYearAsync(memberId, year);
		if (membership == null)
			throw new EntityNotFoundException($"No committee membership found for member {memberId} in year {year}.");

		membership.Position = newPosition;
		await _committeeMembershipRepository.UpdateAsync(membership);
	}
}
