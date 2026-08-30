using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Localization.Resources;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Validates member create/update request fields against domain rules and settings constraints.
/// </summary>
public partial class MemberValidationService
{
    private readonly AgeCalculationService _ageCalc;
    private readonly IStringLocalizer<ValidationResource> _localizer;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();

    public MemberValidationService(AgeCalculationService ageCalc, IStringLocalizer<ValidationResource> localizer)
    {
        _ageCalc = ageCalc;
        _localizer = localizer;
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
            throw new ValidationException(_localizer["Validation_Member_FirstNameRequired"], "Member", operationContext);

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ValidationException(_localizer["Validation_Member_LastNameRequired"], "Member", operationContext);

        if (firstName.Trim().Length > 100)
            throw new ValidationException(_localizer["Validation_Member_FirstNameMaxLength"], "Member", operationContext);

        if (lastName.Trim().Length > 100)
            throw new ValidationException(_localizer["Validation_Member_LastNameMaxLength"], "Member", operationContext);

        if (string.IsNullOrWhiteSpace(streetAddress))
            throw new ValidationException(_localizer["Validation_Member_StreetAddressRequired"], "Member", operationContext);

        if (!string.IsNullOrEmpty(email) && !EmailRegex().IsMatch(email))
            throw new ValidationException(_localizer["Validation_Member_EmailInvalid"], "Member", operationContext);

        _ageCalc.ValidateDateOfBirth(dob, DateTime.UtcNow.Date, settings.MaxAgeRangeYears, settings.MinimumMemberAge);
    }
}
