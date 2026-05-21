namespace StageFright.Maui.Services;

using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Services;
using StageFright.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Service for member-related business logic and CRUD operations.</summary>
public class MemberService : IMemberService
{
	private readonly Data.Repositories.IMemberRepository _memberRepository;
	private readonly AgeCalculationService _ageCalculationService;
	private readonly MemberValidationService _validationService;

	public MemberService(
		Data.Repositories.IMemberRepository memberRepository,
		AgeCalculationService ageCalculationService,
		MemberValidationService validationService)
	{
		_memberRepository = memberRepository;
		_ageCalculationService = ageCalculationService;
		_validationService = validationService;
	}

	public async Task<Member> CreateMemberAsync(Member member)
	{
		// Validate input
		if (string.IsNullOrWhiteSpace(member.Name))
			throw new ValidationException("Member name is required.");

		if (string.IsNullOrWhiteSpace(member.StreetAddress))
			throw new ValidationException("Street address is required.");

		if (member.JoinDate > DateTime.Today)
			throw new ValidationException("Join date cannot be in the future.");

		if (member.DateOfBirth.HasValue && member.DateOfBirth > DateTime.Today)
			throw new ValidationException("Date of birth cannot be in the future.");

		// Set default status to Active
		member.Status = "Active";
		member.ActivateDate = DateTime.Today;

		await _memberRepository.CreateAsync(member);
		return member;
	}

	public async Task<Member?> GetMemberByIdAsync(Guid id)
	{
		return await _memberRepository.GetByIdAsync(id);
	}

	public async Task<IEnumerable<Member>> GetActiveMembersAsync()
	{
		return await _memberRepository.GetActiveMembersAsync();
	}

	public async Task<IEnumerable<Member>> GetInactiveMembersAsync()
	{
		return await _memberRepository.GetInactiveMembersAsync();
	}

	public async Task UpdateMemberAsync(Member member)
	{
		if (member.Id == Guid.Empty)
			throw new ValidationException("Member ID is required.");

		var existing = await _memberRepository.GetByIdAsync(member.Id);
		if (existing == null)
			throw new EntityNotFoundException($"Member with ID {member.Id} not found.");

		// Preserve immutable fields
		member.JoinDate = existing.JoinDate;
		member.IsDeleted = existing.IsDeleted;
		member.DeletedAt = existing.DeletedAt;
		member.DeletedBy = existing.DeletedBy;

		await _memberRepository.UpdateAsync(member);
	}

	public async Task InactivateMemberAsync(Guid id)
	{
		var member = await _memberRepository.GetByIdAsync(id);
		if (member == null)
			throw new EntityNotFoundException($"Member with ID {id} not found.");

		if (member.Status == "Inactive")
			return; // Already inactive

		member.Status = "Inactive";
		member.InactivateDate = DateTime.Today;

		await _memberRepository.UpdateAsync(member);
	}

	public async Task ActivateMemberAsync(Guid id)
	{
		var member = await _memberRepository.GetByIdAsync(id);
		if (member == null)
			throw new EntityNotFoundException($"Member with ID {id} not found.");

		if (member.Status == "Active")
			return; // Already active

		member.Status = "Active";
		member.ActivateDate = DateTime.Today;
		member.InactivateDate = null;

		await _memberRepository.UpdateAsync(member);
	}

	public async Task<int> GetActiveMemberCountAsync()
	{
		return await _memberRepository.GetActiveMemberCountAsync();
	}

	public async Task<int> CalculateAgeAsync(DateTime dateOfBirth)
	{
		return _ageCalculationService.CalculateAge(dateOfBirth);
	}
}
