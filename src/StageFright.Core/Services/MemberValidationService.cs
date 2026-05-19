namespace StageFright.Core.Services;

using Entities;
using System;

/// <summary>Service for member validation.</summary>
public class MemberValidationService
{
	private readonly AgeCalculationService _ageCalculationService;

	public MemberValidationService(AgeCalculationService ageCalculationService)
	{
		_ageCalculationService = ageCalculationService;
	}

	public void ValidateMember(Member member, int maxAge = 150, int minAge = 0)
	{
		if (string.IsNullOrWhiteSpace(member.Name))
			throw new Exceptions.ValidationException("Member name is required.");

		if (string.IsNullOrWhiteSpace(member.StreetAddress))
			throw new Exceptions.ValidationException("Street address is required.");

		if (member.JoinDate > DateTime.Now)
			throw new Exceptions.ValidationException("Join date cannot be in the future.");

		if (member.DateOfBirth.HasValue)
		{
			if (member.DateOfBirth.Value > DateTime.Now)
				throw new Exceptions.ValidationException("Date of birth cannot be in the future.");

			_ageCalculationService.CalculateAge(member.DateOfBirth.Value, maxAge, minAge);
		}

		if (!string.IsNullOrEmpty(member.Email) && !IsValidEmail(member.Email))
			throw new Exceptions.ValidationException("Email format is invalid.");

		if (!string.IsNullOrEmpty(member.Phone) && !IsValidPhone(member.Phone))
			throw new Exceptions.ValidationException("Phone format is invalid.");
	}

	private bool IsValidEmail(string email)
	{
		try
		{
			var addr = new System.Net.Mail.MailAddress(email);
			return addr.Address == email;
		}
		catch
		{
			return false;
		}
	}

	private bool IsValidPhone(string phone)
	{
		var cleaned = System.Text.RegularExpressions.Regex.Replace(phone, @"\D", "");
		return cleaned.Length >= 10;
	}
}
