using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Calculates a member's age in completed years.
/// Handles Feb-29 birthdays in non-leap years by treating Mar 1 as the anniversary.
/// </summary>
public class AgeCalculationService
{
    /// <summary>
    /// Returns age in whole years, or null when dob is null.
    /// </summary>
    public int? Calculate(DateTime? dob, DateTime today)
    {
        if (dob is null) return null;

        var d = dob.Value;
        DateTime birthdayThisYear;
        try
        {
            birthdayThisYear = new DateTime(today.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Feb 29 DOB in a non-leap year → anniversary falls on Mar 1
            birthdayThisYear = new DateTime(today.Year, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        var age = today.Year - d.Year;
        if (today < birthdayThisYear)
            age--;

        return age;
    }

    /// <summary>
    /// Validates a date-of-birth value against system constraints.
    /// No-ops when dob is null (DOB is optional).
    /// </summary>
    public void ValidateDateOfBirth(DateTime? dob, DateTime today, int maxAgeRangeYears, int minimumMemberAge)
    {
        if (dob is null) return;

        if (dob.Value >= today)
            throw new ValidationException(
                "Date of birth must be in the past.",
                "Member", nameof(ValidateDateOfBirth));

        var age = Calculate(dob, today)!.Value;

        if (age > maxAgeRangeYears)
            throw new ValidationException(
                $"Date of birth implies an age of {age} years, which exceeds the maximum allowed range of {maxAgeRangeYears} years.",
                "Member", nameof(ValidateDateOfBirth));

        if (minimumMemberAge > 0 && age < minimumMemberAge)
            throw new ValidationException(
                $"Member must be at least {minimumMemberAge} years of age.",
                "Member", nameof(ValidateDateOfBirth));
    }
}
