namespace StageFright.Core.Services;

using Entities;
using System;
using System.Threading.Tasks;

/// <summary>Service for age calculation with range validation.</summary>
public class AgeCalculationService
{
	private const int DefaultMaxAge = 150;
	private const int DefaultMinAge = 0;

	public int CalculateAge(DateTime dateOfBirth, int maxAgeRange = DefaultMaxAge, int minAge = DefaultMinAge)
	{
		var today = DateTime.Today;
		var age = today.Year - dateOfBirth.Year;

		if (dateOfBirth.Date > today.AddYears(-age))
			age--;

		if (age < minAge)
			throw new Exceptions.ValidationException($"Age {age} is below minimum required age {minAge}.");

		if (age > maxAgeRange)
			throw new Exceptions.ValidationException($"Age {age} exceeds maximum allowed age range {maxAgeRange}.");

		return age;
	}
}

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

/// <summary>Service for GL account assignment.</summary>
public class GLAccountAssignmentService
{
	private static readonly object _lockObject = new object();

	/// <summary>Assigns next available GL account number based on category type.</summary>
	public string AssignGLAccount(string categoryType)
	{
		lock (_lockObject)
		{
			return categoryType switch
			{
				"Income" => $"10{DateTime.Now.Ticks % 100:D2}",
				"Expense" => $"20{DateTime.Now.Ticks % 100:D2}",
				_ => throw new Exceptions.ValidationException("Invalid category type.")
			};
		}
	}
}
