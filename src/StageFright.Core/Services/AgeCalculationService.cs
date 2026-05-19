namespace StageFright.Core.Services;

using System;

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
