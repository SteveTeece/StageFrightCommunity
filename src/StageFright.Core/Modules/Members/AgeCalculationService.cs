using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Calculates a member's age in completed years.
/// Handles Feb-29 birthdays in non-leap years by treating Mar 1 as the anniversary.
/// </summary>
public class AgeCalculationService
{
    private readonly ILocalizer _localizer;

    public AgeCalculationService(ILocalizer localizer)
    {
        _localizer = localizer;
    }

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
                _localizer.Get<ValidationResource>("Validation_Member_DateOfBirthInPast"),
                "Member", nameof(ValidateDateOfBirth));

        var age = Calculate(dob, today)!.Value;

        if (age > maxAgeRangeYears)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Member_AgeExceedsMaxRange", age, maxAgeRangeYears),
                "Member", nameof(ValidateDateOfBirth));

        if (minimumMemberAge > 0 && age < minimumMemberAge)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Member_BelowMinimumAge", minimumMemberAge),
                "Member", nameof(ValidateDateOfBirth));
    }
}
