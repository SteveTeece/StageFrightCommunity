using System.Text.RegularExpressions;
using StageFright.Core.Exceptions;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Validates member create/update request fields against domain rules and settings constraints.
/// </summary>
public partial class MemberValidationService
{
    private readonly AgeCalculationService _ageCalc;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();

    public MemberValidationService(AgeCalculationService ageCalc)
    {
        _ageCalc = ageCalc;
    }

    public void Validate(CreateMemberRequest request, SettingsEntity settings)
    {
        ValidateCommon(request.FirstName, request.LastName, request.StreetAddress, request.Email, request.DateOfBirth, settings, "CreateAsync");
    }

    public void Validate(UpdateMemberRequest request, SettingsEntity settings)
    {
        ValidateCommon(request.FirstName, request.LastName, request.StreetAddress, request.Email, request.DateOfBirth, settings, "UpdateAsync");
    }

    private void ValidateCommon(
        string firstName, string lastName, string streetAddress, string? email, DateTime? dob,
        SettingsEntity settings, string operationContext)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ValidationException("First name is required.", "Member", operationContext);

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ValidationException("Last name is required.", "Member", operationContext);

        if (firstName.Trim().Length > 100)
            throw new ValidationException("First name must be 100 characters or fewer.", "Member", operationContext);

        if (lastName.Trim().Length > 100)
            throw new ValidationException("Last name must be 100 characters or fewer.", "Member", operationContext);

        if (string.IsNullOrWhiteSpace(streetAddress))
            throw new ValidationException("Street address is required.", "Member", operationContext);

        if (!string.IsNullOrEmpty(email) && !EmailRegex().IsMatch(email))
            throw new ValidationException("Email format is invalid.", "Member", operationContext);

        _ageCalc.ValidateDateOfBirth(dob, DateTime.UtcNow.Date, settings.MaxAgeRangeYears, settings.MinimumMemberAge);
    }
}
